# --------------------------------------------------------------------------
# Gurux Ltd
#
# Copyright (c) Gurux Ltd.
#
# This file is part of Gurux Device Framework.
#
# Commit: $Format:%H$
# Date:   $Format:%cI$
#
# This software is available under either of the following licenses:
#
# 1. GNU General Public License, version 2 only (GPL-2.0-only).
#    See the LICENSE file included with this distribution.
#
# 2. A commercial license granted by Gurux Ltd under a separate written
#    agreement. Use under this option requires a valid commercial license
#    and is governed by the terms of that agreement.
#
# If you have not obtained a commercial license from Gurux Ltd,
# use of this software is governed by GPL-2.0-only.
#
# Commercial licensing: https://www.gurux.fi/
#
# SPDX-License-Identifier: GPL-2.0-only OR LicenseRef-Gurux-Commercial
# --------------------------------------------------------------------------

"""Tag changed modules on a branch push and explicitly dispatch their releases."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import subprocess


def git(root, *args):
    return subprocess.check_output(['git', *args], cwd=root, text=True, encoding='utf-8').strip()


def plan_tags(root, before, revision, *, today=None):
    today = today if today is not None else datetime.now(timezone.utc).date()
    month = (today.year % 100) * 100 + today.month
    first_revision = today.day * 100 + 1
    root = Path(root)
    modules = json.loads((root / 'modules.json').read_text(encoding='utf-8'))['modules']
    target = git(root, 'rev-parse', revision)
    if before == '0' * 40:
        changed = git(root, 'ls-tree', '-r', '--name-only', target).splitlines()
    else:
        changed = git(root, 'diff', '--name-only', '--no-renames', before, target).splitlines()
    reachable = git(root, 'tag', '--merged', target).splitlines()
    tags = []
    for identifier, module in sorted(modules.items()):
        paths = module['changelogPaths']
        versions = []
        for tag in reachable:
            match = re.fullmatch(re.escape(identifier) + r'/v([0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)', tag)
            if match:
                versions.append((tuple(int(part) for part in match[1].split('.')), tag))
        # Reuse tags from a partially completed run; never move a release tag.
        existing = [item for item in versions
                    if git(root, 'rev-list', '-n', '1', item[1]) == target]
        module_changes = changed
        if not versions:
            # No baseline exists: all tracked module sources are unreleased,
            # including sources committed before automation was introduced.
            module_changes = git(root, 'ls-tree', '-r', '--name-only', target).splitlines()
        elif not existing:
            # A previous push may have failed before tagging. Compare with the
            # last module tag so a workflow-only fix can recover those changes.
            module_changes = git(root, 'diff', '--name-only', '--no-renames',
                                 max(versions)[1], target).splitlines()
        if not any(file == path or file.startswith(path.rstrip('/') + '/')
                   for file in module_changes for path in paths):
            continue
        if existing:
            tags.append(max(existing)[1])
            continue
        latest = max(versions)[0] if versions else None
        major, minor = latest[:2] if latest else (1, 0)
        version = [major, minor, month, first_revision]
        if latest:
            previous = (*latest, 0) if len(latest) == 3 else latest
            if previous[2] > month or (previous[2] == month and previous[3] // 100 > today.day):
                raise ValueError(f'Release date precedes the latest version for {identifier}')
            if previous[2] == month and previous[3] // 100 == today.day:
                if previous[3] % 100 >= 99:
                    raise ValueError(f'Maximum 99 releases per day reached for {identifier}')
                version[3] = max(first_revision, previous[3] + 1)
        tags.append(identifier + '/v' + '.'.join(map(str, version)))
    return tags


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--before', required=True)
    parser.add_argument('--revision', required=True)
    parser.add_argument('--publish', action='store_true', help='Create remote tags and dispatch releases')
    args = parser.parse_args()
    root = Path.cwd()
    target = git(root, 'rev-parse', args.revision)
    tags = plan_tags(root, args.before, target)
    print(json.dumps(tags))
    if not args.publish:
        return
    repository = os.environ['GITHUB_REPOSITORY']
    existing = set(git(root, 'tag', '--list').splitlines())
    for tag in tags:
        if tag not in existing:
            subprocess.run(['gh', 'api', '--method', 'POST', f'repos/{repository}/git/refs',
                            '-f', f'ref=refs/tags/{tag}', '-f', f'sha={target}'], check=True)
        # GITHUB_TOKEN-created tag pushes do not trigger another workflow.
        subprocess.run(['gh', 'workflow', 'run', 'release-module.yml', '--repo', repository,
                        '--ref', os.environ['GITHUB_REF_NAME'], '-f', f'tag={tag}'], check=True)


if __name__ == '__main__':
    main()
