"""Build or verify immutable module content releases, then notify Catalog."""
import argparse
import json
import os
from pathlib import Path
import subprocess


def run(*args, **kwargs):
    return subprocess.run(list(args), check=True, text=True, encoding='utf-8', **kwargs)


def publish(tool, output):
    output = Path(output).resolve()
    release = json.loads((output / 'release.json').read_text(encoding='utf-8'))
    repo = os.environ['GITHUB_REPOSITORY']
    token = os.environ.get('CATALOG_TOKEN')
    if not token:
        raise ValueError('Missing CATALOG_DISPATCH_TOKEN')
    tag = release['tag']
    # API failures other than a genuinely missing release must fail closed.
    response = run('gh', 'api', f'repos/{repo}/releases', '--paginate', '--slurp', capture_output=True)
    existing = next((item for page in json.loads(response.stdout) for item in page if item['tag_name'] == tag), None)
    stem = f"{release['id']}-{release['packageType']}-{release['version']}"
    package_name = stem + ('.json' if release['kind'] == 'localization' else '.zip')
    manifest_name = 'release-manifest.json'
    if existing and not existing['draft']:
        names = {asset['name'] for asset in existing['assets']}
        if not {package_name, manifest_name}.issubset(names):
            raise ValueError('Published content release is missing its manifest or package')
        downloaded = output / 'published'
        downloaded.mkdir(exist_ok=True)
        run('gh', 'release', 'download', tag, '--repo', repo, '--dir', str(downloaded),
            '--pattern', manifest_name, '--pattern', package_name, '--clobber')
        manifest, package = downloaded / manifest_name, downloaded / package_name
    else:
        built = run('dotnet', str(tool), 'content', 'build-module', '--sources', str(output / 'sources.json'),
                    '--kind', release['kind'], '--version', release['version'], '--output', str(output / 'content'), capture_output=True)
        entries = json.loads(built.stdout)
        if len(entries) != 1 or entries[0]['tag'] != tag:
            raise ValueError('Content builder returned an unexpected release')
        manifest, package = Path(entries[0]['manifest']), Path(entries[0]['package'])
        if manifest.name != manifest_name or package.name != package_name:
            raise ValueError('Content builder returned unexpected asset names')
    metadata = json.loads(manifest.read_text(encoding='utf-8-sig'))
    if (metadata.get('modulePackage') is not True or metadata.get('owner') != release['id']
            or metadata.get('ownerType') != 'module' or metadata.get('kind') != release['kind']
            or metadata.get('version') != release['version']
            or metadata.get('unit') != ('all' if release['kind'] == 'localization' else 'asset')
            or metadata.get('ownerVersionRange') != json.loads((output / 'sources.json').read_text())['sources'][0]['ownerVersionRange']):
        raise ValueError('Manifest does not match release tag and source configuration')
    run('dotnet', str(tool), 'content', 'verify', '--manifest', str(manifest), '--package', str(package))
    if not existing or existing['draft']:
        if not existing:
            args = ['gh', 'release', 'create', tag, '--repo', repo, '--verify-tag', '--draft',
                    '--title', f"{release['id']} {release['packageType']} {release['version']}",
                    '--notes', f"Independent {release['packageType']} package for {release['id']}."]
            if release['prerelease']:
                args.append('--prerelease')
            run(*args)
        # Recheck immediately before upload: retries never replace published assets.
        state = json.loads(run('gh', 'release', 'view', tag, '--repo', repo, '--json', 'isDraft', capture_output=True).stdout)
        if not state['isDraft']:
            raise ValueError('Release became published; retry to verify published assets')
        run('gh', 'release', 'upload', tag, str(manifest), str(package), '--repo', repo, '--clobber')
        run('gh', 'release', 'edit', tag, '--repo', repo, '--draft=false', '--latest=false')
    payload = {'event_type': release['kind'] + '-release', 'client_payload': {
        'module': release['id'], 'packageType': release['packageType'], 'tag': tag, 'repository': repo}}
    env = dict(os.environ, GH_TOKEN=token)
    run('gh', 'api', '--method', 'POST', 'repos/Gurux/Gurux.AMI.Catalog/dispatches', '--input', '-',
        input=json.dumps(payload), env=env)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tool', required=True)
    parser.add_argument('--output', default='release')
    args = parser.parse_args()
    publish(Path(args.tool).resolve(), args.output)
