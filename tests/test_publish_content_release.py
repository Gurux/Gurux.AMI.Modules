import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
import publish_content_release as publisher


class PublishContentTests(unittest.TestCase):
    def exercise(self, published=False, fail_verify=False):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            tag = 'Gurux.AMI.SMTP/assets/v1.0.3'
            package = 'Gurux.AMI.SMTP-assets-1.0.3.zip'
            (root / 'release.json').write_text(json.dumps(dict(id='Gurux.AMI.SMTP', tag=tag, version='1.0.3', kind='resources', packageType='assets', prerelease=False)))
            (root / 'sources.json').write_text(json.dumps({'sources': [{'ownerVersionRange': '[1.0.0,2.0.0)'}]}))
            calls = []

            def artifacts(directory):
                directory.mkdir(exist_ok=True)
                manifest = directory / 'release-manifest.json'
                manifest.write_text(json.dumps(dict(modulePackage=True, owner='Gurux.AMI.SMTP', ownerType='module', kind='resources', version='1.0.3', unit='asset', ownerVersionRange='[1.0.0,2.0.0)')))
                (directory / package).write_bytes(b'package')
                return manifest

            def fake(*args, **kwargs):
                calls.append(args)
                output = ''
                if args[:3] == ('gh', 'api', 'repos/Test/Modules/releases'):
                    output = json.dumps([[dict(tag_name=tag, draft=False, assets=[{'name': package}, {'name': 'release-manifest.json'}])]] if published else [[]])
                elif 'build-module' in args:
                    manifest = artifacts(root / 'content')
                    output = json.dumps([dict(manifest=str(manifest), package=str(manifest.parent / package), tag=tag)])
                elif args[:3] == ('gh', 'release', 'download'):
                    artifacts(root / 'published')
                elif 'verify' in args and fail_verify:
                    raise subprocess.CalledProcessError(1, args)
                elif args[:3] == ('gh', 'release', 'view'):
                    output = '{"isDraft":true}'
                return subprocess.CompletedProcess(args, 0, stdout=output)

            with patch.dict(os.environ, GITHUB_REPOSITORY='Test/Modules', CATALOG_TOKEN='token'), patch.object(publisher, 'run', side_effect=fake):
                if fail_verify:
                    with self.assertRaises(subprocess.CalledProcessError):
                        publisher.publish(root / 'tool.dll', root)
                else:
                    publisher.publish(root / 'tool.dll', root)
            return calls

    def test_new_release_builds_verifies_uploads_then_dispatches(self):
        calls = self.exercise()
        self.assertTrue(any('build-module' in call for call in calls))
        self.assertTrue(any(call[:3] == ('gh', 'release', 'upload') for call in calls))
        self.assertIn('repos/Gurux/Gurux.AMI.Catalog/dispatches', calls[-1])

    def test_published_retry_downloads_verifies_without_build_or_upload(self):
        calls = self.exercise(published=True)
        self.assertTrue(any(call[:3] == ('gh', 'release', 'download') for call in calls))
        self.assertTrue(any('verify' in call for call in calls))
        self.assertFalse(any('build-module' in call or 'upload' in call or 'edit' in call for call in calls))

    def test_failed_published_verification_does_not_dispatch(self):
        calls = self.exercise(published=True, fail_verify=True)
        self.assertFalse(any('repos/Gurux/Gurux.AMI.Catalog/dispatches' in call for call in calls))
