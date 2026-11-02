"""Update existing AMI NuGet references and verify their project graph.

No publish, pack, tag or release commands belong in this tool.
"""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import time
import urllib.request
import xml.etree.ElementTree as ET

PACKAGES = ('Gurux.DLMS.AMI.Shared', 'Gurux.DLMS.AMI.Module')
TARGETS = {p.lower(): p for p in PACKAGES}


def version_key(value):
    match = re.fullmatch(r'(\d+(?:\.\d+){0,3})(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?', value)
    if not match:
        raise ValueError(f'Expected a literal NuGet version, got {value!r}; update this reference manually.')
    numbers = tuple(int(n) for n in match[1].split('.'))
    numbers += (0,) * (4 - len(numbers))
    pre = tuple((0, int(p)) if p.isdigit() else (1, p.lower()) for p in (match[2] or '').split('.'))
    return numbers, match[2] is None, pre


def fetch_versions(package):
    url = f'https://api.nuget.org/v3-flatcontainer/{package.lower()}/index.json'
    with urllib.request.urlopen(url, timeout=30) as response:
        return json.load(response)['versions']


def latest_stable(package, expected='', attempts=5, delay=20):
    # Dispatch payload is only a propagation hint; never use it as a download URL/version source.
    if expected and '-' in expected:
        expected = ''
    if expected:
        version_key(expected)
    error = None
    for attempt in range(attempts):
        try:
            stable = [v for v in fetch_versions(package) if '-' not in v.split('+')[0]]
            latest = max(stable, key=version_key)
            if not expected or version_key(latest) >= version_key(expected):
                return latest
            error = ValueError(f'{latest} is older than announced version {expected}')
        except (OSError, ValueError, KeyError) as exc:
            error = exc
        if attempt + 1 < attempts:
            print(f'{package}: retrying NuGet index ({attempt + 1}/{attempts}): {error}', flush=True)
            time.sleep(delay)
    raise RuntimeError(f'NuGet index unavailable or publication has not propagated for {package}: {error}')


def nodes(tree, name):
    return [n for n in tree.iter() if n.tag.split('}')[-1] == name]


def metadata(node, name):
    return node.get(name) or next((n.text or '' for n in node if n.tag.split('}')[-1] == name), '')


def discover(root):
    graph = {}
    if (root / '.git').exists():
        tracked = subprocess.run(['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z', '--', '*.csproj'],
                                 cwd=root, check=True, capture_output=True).stdout.decode('utf-8')
        projects = {root / p for p in tracked.split('\0') if p}
    else:
        projects = set(root.rglob('*.csproj'))
    for project in sorted(projects):
        if any(p in {'.git', 'obj', 'bin'} for p in project.relative_to(root).parts):
            continue
        tree = ET.parse(project)
        refs = nodes(tree, 'PackageReference')
        central = next((d / 'Directory.Packages.props' for d in [project.parent, *project.parent.parents]
                        if d.is_relative_to(root) and (d / 'Directory.Packages.props').is_file()), None)
        lock = next((n.text for n in nodes(tree, 'NuGetLockFilePath') if n.text), 'packages.lock.json')
        lock = lock.replace('$(MSBuildProjectDirectory)', str(project.parent))
        if '$(' in lock:
            raise ValueError(f'Unsupported lock file property in {project}: {lock}')
        references = {(project.parent / n.get('Include').replace('\\', '/')).resolve()
                      for n in nodes(tree, 'ProjectReference') if n.get('Include')}
        if any(not p.is_relative_to(root) for p in references):
            raise ValueError(f'Project reference outside repository: {project}')
        graph[project] = {
            'refs': references,
            'packages': {TARGETS[n.get('Include', n.get('Update', '')).lower()] for n in refs
                         if n.get('Include', n.get('Update', '')).lower() in TARGETS},
            'test': any((n.text or '').lower() == 'true' for n in nodes(tree, 'IsTestProject'))
                    or any(n.get('Include', '').lower() == 'microsoft.net.test.sdk' for n in refs),
            'lock': (project.parent / lock).resolve(),
            'central': central,
            'versioned': {TARGETS[n.get('Include', n.get('Update', '')).lower()] for n in refs
                          if n.get('Include', n.get('Update', '')).lower() in TARGETS
                          and (metadata(n, 'Version') or metadata(n, 'VersionOverride'))},
        }
    return graph


def update_xml(text, versions):
    changes = []
    candidates = {p.lower(): v for p, v in versions.items()}
    # Work on one complete XML item at a time to preserve whitespace, comments,
    # unrelated properties and conditional entries. The XML parser verifies identities.
    pattern = r'<!--.*?-->|<(PackageReference|PackageVersion)\b[^>]*?(?:/>|>.*?</\1\s*>)'

    def replace(match):
        item = match[0]
        if item.startswith('<!--'):
            return item
        node = ET.fromstring(item)
        package = node.get('Include', node.get('Update', ''))
        if package.lower() not in candidates:
            return item
        new = candidates[package.lower()]
        field = 'VersionOverride' if metadata(node, 'VersionOverride') else 'Version'
        old = metadata(node, field).strip()
        if not old:
            return item
        if version_key(new) <= version_key(old):
            return item
        if field in node.attrib:
            item = re.sub(rf'(\b{field}\s*=\s*)([\'\"])(.*?)\2',
                          lambda m: m[1] + m[2] + new + m[2], item, count=1, flags=re.S)
        else:
            item = re.sub(rf'(<{field}\b[^>]*>)(.*?)(</{field}>)',
                          lambda m: m[1] + new + m[3], item, count=1, flags=re.S)
        changes.append((TARGETS[package.lower()], old, new))
        return item

    return re.sub(pattern, replace, text, flags=re.S), changes


def apply_updates(root, graph, versions):
    sources = {}
    for project, info in graph.items():
        for package in info['packages']:
            source = project if package in info['versioned'] else info['central']
            if source is None:
                raise ValueError(f'No literal version or Directory.Packages.props found for {package} in {project}')
            sources.setdefault(source, {}).setdefault(package, set()).add(project)
    changes, files, seeds = [], set(), set()
    for source, consumers in sources.items():
        entries = [n for n in ET.parse(source).iter() if n.tag.split('}')[-1] in {'PackageReference', 'PackageVersion'}]
        for package in consumers:
            if not any(n.get('Include', n.get('Update', '')).lower() == package.lower()
                       and (metadata(n, 'VersionOverride') or metadata(n, 'Version')) for n in entries):
                raise ValueError(f'No literal version found for {package} in {source}; imported/property-based versions require a manual update.')
        raw = source.read_bytes()
        text = raw.decode('utf-8-sig')
        updated, edits = update_xml(text, {p: versions[p] for p in consumers if p in versions})
        if edits:
            prefix = b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b''
            source.write_bytes(prefix + updated.encode('utf-8'))
            files.add(source)
            for package, old, new in edits:
                seeds.update(consumers[package])
                changes.append({'package': package, 'from': old, 'to': new, 'file': source.relative_to(root).as_posix()})
    return changes, files, seeds


def dependents(graph, seeds):
    result = set(seeds)
    while True:
        more = {p for p, info in graph.items() if info['refs'] & result} - result
        if not more:
            return result
        result.update(more)


def closure(graph, projects):
    result = set(projects)
    while True:
        more = {r for p in result for r in graph[p]['refs']} - result
        if not more:
            return result
        if not more <= graph.keys():
            raise ValueError(f'Unresolved project references: {more - graph.keys()}')
        result.update(more)


def dotnet(*args):
    print('dotnet ' + ' '.join(str(a) for a in args), flush=True)
    subprocess.run(['dotnet', *map(str, args)], check=True)


def refresh_locks(graph, projects):
    files = set()
    for project in sorted(closure(graph, projects)):
        dotnet('restore', project, '--force-evaluate', '--use-lock-file')
        lock = graph[project]['lock']
        if not lock.is_file():
            raise RuntimeError(f'Restore did not create expected lock file: {lock}')
        files.add(lock)
    return files


def validate(graph, projects):
    for project in sorted(closure(graph, projects)):
        if not graph[project]['lock'].is_file():
            raise RuntimeError(f'Missing lock file: {graph[project]["lock"]}; run -Mode Lock first.')
    # Restore/build each project immediately before use: multiple projects may share a directory.
    for project in sorted(projects):
        dotnet('restore', project, '--locked-mode', '--use-lock-file')
        dotnet('build', project, '--configuration', 'Release', '--no-restore', '-p:GeneratePackageOnBuild=false')
        if graph[project]['test']:
            dotnet('test', project, '--configuration', 'Release', '--no-build', '--no-restore')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode', choices=['Update', 'Validate', 'Lock'], default='Validate')
    parser.add_argument('--root', type=Path, default=Path.cwd())
    parser.add_argument('--result', type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    graph = discover(root)
    changes, files = [], set()
    seeds = {p for p, info in graph.items() if info['packages']}
    if args.mode == 'Update':
        payload = {}
        if os.environ.get('GITHUB_EVENT_NAME') == 'repository_dispatch':
            event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text(encoding='utf-8-sig'))
            payload = event.get('client_payload', {})
        versions = {p: latest_stable(p, str(payload.get('version', '')) if payload.get('package') == p else '') for p in PACKAGES}
        changes, files, seeds = apply_updates(root, graph, versions)
    affected = dependents(graph, seeds)
    if args.mode == 'Lock' or changes:
        files.update(refresh_locks(graph, affected))
    if args.mode != 'Update' or changes:
        validate(graph, affected)
    modules = sorted(p.relative_to(root).as_posix() for p in affected if not graph[p]['test'])
    tests = sorted(p.relative_to(root).as_posix() for p in affected if graph[p]['test'])
    body = ['## AMI dependency update', '', '| Package | From | To | Reference |', '|---|---|---|---|']
    body += [f'| {c["package"]} | {c["from"]} | {c["to"]} | `{c["file"]}` |' for c in changes]
    body += ['', 'Affected modules (including project-reference dependents):', *[f'- `{p}`' for p in modules],
             '', 'Related tests:', *([f'- `{p}`' for p in tests] or ['- No related test projects found.']), '',
             'Verified in the trusted update workflow before pushing: `dotnet restore --locked-mode`,',
             '`dotnet build --configuration Release --no-restore`, and related `dotnet test`.',
             '', 'Module versions and release tags are unchanged. Merge, then publish each module separately with a new module tag.',
             '', f'Workflow run: {os.environ.get("GITHUB_SERVER_URL", "https://github.com")}/{os.environ.get("GITHUB_REPOSITORY", "")}/actions/runs/{os.environ.get("GITHUB_RUN_ID", "")}']
    if args.mode == 'Update' and not changes:
        body = ['No newer stable versions for existing AMI references. No files changed; no PR or compatibility build needed.']
    result = {'changed': bool(changes), 'updates': changes, 'filesChanged': sorted(p.relative_to(root).as_posix() for p in files),
              'buildProjects': modules, 'testProjects': tests, 'prBody': '\n'.join(body)}
    if args.result:
        args.result.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
