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

"""Validate module content sources before invoking the Updater content builder."""
import argparse
import json
from pathlib import Path
import re
import stat

from prepare_release import parse_tag

ASSETS = {'.svg', '.png', '.jpg', '.jpeg', '.gif', '.webp', '.ico', '.woff', '.woff2', '.ttf', '.otf', '.css', '.html', '.json', '.xml', '.txt'}
FORBIDDEN = {'bin', 'obj', '.git', '.github', 'node_modules', 'properties', 'secrets'}


def checked_path(root, value):
    if not isinstance(value, str) or not value.strip() or Path(value).is_absolute() or Path(value).drive:
        raise ValueError('Source directory must be an explicit repository-relative path')
    path = root / value
    if not path.resolve().is_relative_to(root):
        raise ValueError('Source directory escapes repository')
    current = root
    for part in Path(value).parts:
        if part in {'.', '..'} or part.lower() in FORBIDDEN:
            raise ValueError('Unsafe source directory')
        current = current / part
        attributes = getattr(current.lstat(), 'st_file_attributes', 0) if current.exists() else 0
        if current.is_symlink() or attributes & getattr(stat, 'FILE_ATTRIBUTE_REPARSE_POINT', 1024):
            raise ValueError('Reparse points and symlinks are not allowed')
    return path.resolve()


def prepare(root, tag):
    root = Path(root).resolve()
    owner, package_type, version = parse_tag(tag)
    if package_type == 'module':
        raise ValueError('A localization or assets tag is required')
    modules = json.loads((root / 'modules.json').read_text(encoding='utf-8'))['modules']
    if owner not in modules:
        raise ValueError(f'Unknown module: {owner}')
    module = modules[owner]
    project = checked_path(root, module['project'])
    if not project.is_file():
        raise ValueError('Module project is missing')
    directories = {}
    for kind in ('localization', 'assets'):
        config = module.get(kind)
        if config is not None:
            directory = checked_path(root, config.get('directory'))
            if directory == project.parent or not directory.is_relative_to(project.parent):
                raise ValueError('Content source must be a dedicated directory inside the module project directory')
            directories[kind] = directory
    if len(directories) == 2:
        left, right = directories.values()
        if left.is_relative_to(right) or right.is_relative_to(left):
            raise ValueError('Localization and assets directories must not overlap')
    if package_type not in directories:
        raise ValueError('Content source is not configured')
    directory = directories[package_type]
    if not directory.is_dir():
        raise ValueError('Content directory does not exist')
    files = []
    for item in directory.rglob('*'):
        checked_path(root, str(item.relative_to(root)))
        if item.is_file():
            if any(part.lower() in FORBIDDEN or part.startswith('.') for part in item.relative_to(directory).parts) or any(word in item.name.lower() for word in ('secret', 'credential', 'appsettings')):
                raise ValueError('Forbidden content path')
            if package_type == 'localization':
                if item.parent != directory or not re.fullmatch(r'[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*\.json', item.name):
                    raise ValueError('Localization allows culture JSON files only')
            elif item.suffix.lower() not in ASSETS:
                raise ValueError('Unsupported asset file')
            files.append(item)
    if not files:
        raise ValueError('Content directory is empty')
    config = module[package_type]
    compatibility = config.get('ownerVersionRange')
    if not isinstance(compatibility, str) or not compatibility.strip():
        raise ValueError('ownerVersionRange is required')
    kind = 'localization' if package_type == 'localization' else 'resources'
    source = dict(kind=kind, directory=str(directory), owner=owner,
                  ownerType='module', ownerVersionRange=compatibility)
    if kind == 'localization':
        source['defaultCulture'] = config.get('defaultCulture', '')
        if (not isinstance(source['defaultCulture'], str)
                or not re.fullmatch(r'[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*', source['defaultCulture'])
                or not (directory / (source['defaultCulture'] + '.json')).is_file()):
            raise ValueError('defaultCulture must identify an existing culture file')
    else:
        source['name'] = 'asset'
    return {'sources': [source]}, dict(id=owner, tag=tag, version=version,
        kind=kind, packageType=package_type, prerelease='-' in version.split('+')[0])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tag', required=True)
    parser.add_argument('--output', default='release')
    args = parser.parse_args()
    sources, release = prepare(Path.cwd(), args.tag)
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    for name, value in [('sources.json', sources), ('release.json', release)]:
        (output / name).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
