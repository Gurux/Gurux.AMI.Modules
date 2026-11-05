"""Opt-in real Updater protocol test, without GitHub calls or module builds."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from prepare_content_release import prepare


@unittest.skipUnless(os.environ.get('GURUX_CONTENT_TOOL'), 'Set GURUX_CONTENT_TOOL to the built Updater Tool DLL')
class ContentIntegrationTests(unittest.TestCase):
    def test_prepare_import_list_and_separate_install_do_not_build_or_change_module(self):
        tool = Path(os.environ['GURUX_CONTENT_TOOL']).resolve()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            owner = 'Gurux.AMI.Example'
            module = root / 'Example'
            (module / 'localized').mkdir(parents=True)
            (module / 'asset/images').mkdir(parents=True)
            # This module deliberately cannot build. A resource-only release must succeed.
            project = '<Project Sdk="Missing.Sdk"><PropertyGroup><Version>42.0.0</Version></PropertyGroup></Project>'
            (module / 'Example.csproj').write_text(project)
            (module / 'localized/en.json').write_text('{"Title":"Mail"}')
            (module / 'localized/fi.json').write_text('{"Title":"Posti"}')
            (module / 'asset/images/mail.svg').write_text('<svg/>')
            (root / 'modules.json').write_text(json.dumps({'modules': {owner: {
                'project': 'Example/Example.csproj', 'changelogPaths': ['Example'],
                'localization': {'directory': 'Example/localized', 'defaultCulture': 'en', 'ownerVersionRange': '[1.0.0,2.0.0)'},
                'assets': {'directory': 'Example/asset', 'ownerVersionRange': '[1.0.0,2.0.0)'},
            }}}))
            def command(*args):
                completed = subprocess.run(['dotnet', str(tool), 'content', *map(str, args)], capture_output=True, text=True)
                self.assertEqual(completed.returncode, 0, completed.stderr + completed.stdout)
                return json.loads(completed.stdout)
            catalog = root / 'catalog'
            install = root / 'installed'
            install.mkdir()
            (install / (owner + '.dll')).write_bytes(b'preserve module binary')
            for package_type, kind, version in [('localization', 'localization', '7.0.0'), ('assets', 'resources', '5.0.0')]:
                sources, release = prepare(root, f'{owner}/{package_type}/v{version}')
                configuration = root / (kind + '-sources.json')
                configuration.write_text(json.dumps(sources))
                built = command('build-module', '--sources', configuration, '--kind', kind, '--version', version, '--output', root / kind)[0]
                package, manifest = Path(built['package']), Path(built['manifest'])
                metadata = json.loads(manifest.read_text())
                self.assertEqual(metadata['ownerVersionRange'], '[1.0.0,2.0.0)')
                self.assertEqual(metadata['tag'], release['tag'])
                if kind == 'localization':
                    language = json.loads(package.read_text())
                    self.assertEqual(language['resources'], {'en': {'Title': 'Mail'}, 'fi': {'Title': 'Posti'}})
                else:
                    with zipfile.ZipFile(package) as archive:
                        self.assertCountEqual(archive.namelist(), ['images/mail.svg', 'resource-package.json'])
                command('import', '--manifest', manifest, '--package', package, '--catalog-root', catalog)
                index = catalog / kind / ('localization-index.json' if kind == 'localization' else 'resource-index.json')
                options = ['--index', index, '--owner', owner, '--kind', kind, '--owner-version', '1.5.0']
                self.assertEqual(command('list-module', *options)[0]['version'], version)
                self.assertTrue(command('update-module', *options, '--install', install)['updated'])
                self.assertFalse(command('check-module', *options, '--install', install)['updateAvailable'])
            self.assertEqual((module / 'Example.csproj').read_text(), project)
            self.assertFalse((module / 'obj').exists())
            self.assertFalse((module / 'bin').exists())
            self.assertEqual((install / (owner + '.dll')).read_bytes(), b'preserve module binary')
            self.assertTrue((install / 'localized' / (owner + '.json')).is_file())
            self.assertEqual((install / 'asset/images/mail.svg').read_text(), '<svg/>')
