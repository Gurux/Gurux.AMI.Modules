import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / 'scripts/create_module_tags.py'


class CreateModuleTagsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.git('init', '-b', 'main')
        self.git('config', 'user.email', 'test@example.com')
        self.git('config', 'user.name', 'Test')
        (self.root / 'modules.json').write_text(json.dumps({'modules': {
            'Gurux.AMI.SMTP': {'changelogPaths': ['SMTP', 'shared.props']},
            'Gurux.AMI.OpenApi': {'changelogPaths': ['OpenApi', 'shared.props']}}}))
        self.commit('SMTP/file', 'initial')
        self.before = self.git('rev-parse', 'HEAD')

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root, text=True).strip()

    def commit(self, file, text):
        path = self.root / file
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text)
        self.git('add', '.')
        self.git('commit', '-m', text)

    def plan(self, before=None):
        spec = importlib.util.spec_from_file_location('create_module_tags', SCRIPT)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module.plan_tags(self.root, before or self.before, 'HEAD')

    def test_changed_module_increments_latest_stable_four_part_version(self):
        self.git('tag', 'Gurux.AMI.SMTP/v4.1.2610.3001')
        self.git('tag', 'Gurux.AMI.SMTP/v99.0.0-rc1')
        self.commit('SMTP/file', 'fix SMTP')
        self.assertEqual(self.plan(), ['Gurux.AMI.SMTP/v4.1.2610.3002'])

    def test_shared_change_tags_both_modules(self):
        self.commit('shared.props', 'shared build fix')
        self.assertEqual(self.plan(), ['Gurux.AMI.OpenApi/v1.0.0', 'Gurux.AMI.SMTP/v1.0.0'])

    def test_unrelated_commit_creates_no_tags(self):
        self.commit('README.md', 'documentation')
        self.assertEqual(self.plan(), [])

    def test_retry_reuses_tag_on_same_commit(self):
        self.commit('SMTP/file', 'fix SMTP')
        self.git('tag', 'Gurux.AMI.SMTP/v1.0.0')
        self.assertEqual(self.plan(), ['Gurux.AMI.SMTP/v1.0.0'])

    def test_multi_commit_push_includes_earlier_module_change(self):
        self.commit('SMTP/file', 'fix SMTP')
        self.commit('README.md', 'documentation')
        self.assertEqual(self.plan(), ['Gurux.AMI.SMTP/v1.0.0'])

    def test_new_branch_push_uses_full_history(self):
        self.assertEqual(self.plan('0' * 40), ['Gurux.AMI.SMTP/v1.0.0'])


if __name__ == '__main__':
    unittest.main()
