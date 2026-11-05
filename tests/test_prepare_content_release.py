import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from prepare_release import parse_tag, prepare_release
from prepare_content_release import prepare


class ContentPreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'SMTP/localized').mkdir(parents=True)
        (self.root / 'SMTP/asset').mkdir()
        (self.root / 'SMTP/module.csproj').write_text('<Project/>')
        (self.root / 'SMTP/localized/en.json').write_text('{"Hello":"Hello"}')
        (self.root / 'SMTP/asset/icon.svg').write_text('<svg/>')
        self.module = {'project': 'SMTP/module.csproj', 'changelogPaths': ['SMTP'],
                       'localization': {'directory': 'SMTP/localized', 'defaultCulture': 'en', 'ownerVersionRange': '[1.0.0,2.0.0)'},
                       'assets': {'directory': 'SMTP/asset', 'ownerVersionRange': '[1.0.0,2.0.0)'}}
        self.save()

    def save(self):
        (self.root / 'modules.json').write_text(json.dumps({'modules': {'Gurux.AMI.SMTP': self.module}}))

    def test_tag_families(self):
        for suffix, kind in [('v1.0.5', 'module'), ('localization/v1.0.2', 'localization'), ('assets/v1.0.3', 'assets')]:
            self.assertEqual(parse_tag('Gurux.AMI.SMTP/' + suffix)[1], kind)
        for tag in ['Gurux.AMI.SMTP/assets/v01.0.0', 'Gurux.AMI.SMTP/resources/v1.0.0', 'Gurux.AMI.SMTP/localization/1.0.0', 'x/v1.0.0']:
            with self.assertRaises(ValueError):
                parse_tag(tag)

    def test_current_module_path_rejects_content_tags(self):
        with self.assertRaises(ValueError):
            prepare_release(self.root, 'Gurux.AMI.SMTP/assets/v1.0.3', [])

    def test_content_sources_are_independent(self):
        for package_type, kind in [('localization', 'localization'), ('assets', 'resources')]:
            sources, release = prepare(self.root, f'Gurux.AMI.SMTP/{package_type}/v1.0.3')
            self.assertEqual(len(sources['sources']), 1)
            self.assertEqual(sources['sources'][0]['kind'], kind)
            self.assertEqual(release['version'], '1.0.3')

    def test_unknown_module(self):
        with self.assertRaises(ValueError):
            prepare(self.root, 'Gurux.AMI.Other/assets/v1.0.3')

    def test_missing_empty_or_unsafe_sources(self):
        for directory in ['', '../outside', 'SMTP', 'SMTP/obj', 'SMTP/missing', 'SMTP/localized']:
            self.module['assets']['directory'] = directory
            self.save()
            with self.subTest(directory=directory), self.assertRaises(ValueError):
                prepare(self.root, 'Gurux.AMI.SMTP/assets/v1.0.3')

    def test_forbidden_assets(self):
        for name in ['code.cs', 'library.dll', 'appsettings.json', 'credentials.txt', '.env']:
            file = self.root / 'SMTP/asset' / name
            file.write_text('secret')
            with self.subTest(name=name), self.assertRaises(ValueError):
                prepare(self.root, 'Gurux.AMI.SMTP/assets/v1.0.3')
            file.unlink()

    def test_empty_directory(self):
        (self.root / 'SMTP/asset/icon.svg').unlink()
        with self.assertRaises(ValueError):
            prepare(self.root, 'Gurux.AMI.SMTP/assets/v1.0.3')

    def test_symlink(self):
        link = self.root / 'SMTP/asset/link.svg'
        try:
            link.symlink_to(self.root / 'SMTP/asset/icon.svg')
        except OSError:
            self.skipTest('Symlinks unavailable')
        with self.assertRaises(ValueError):
            prepare(self.root, 'Gurux.AMI.SMTP/assets/v1.0.3')
