from datetime import date
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

    def plan(self, before=None, today=date(2026, 11, 2)):
        spec = importlib.util.spec_from_file_location('create_module_tags', SCRIPT)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module.plan_tags(self.root, before or self.before, 'HEAD', today=today)

    def test_changed_module_uses_date_and_preserves_major_minor(self):
        self.git('tag', 'Gurux.AMI.SMTP/v4.1.2610.3001')
        self.git('tag', 'Gurux.AMI.SMTP/v99.0.0-rc1')
        self.commit('SMTP/file', 'fix SMTP')
        self.assertEqual(self.plan(), ['Gurux.AMI.SMTP/v4.1.2611.201'])

    def test_same_day_increments_final_two_digits(self):
        self.git('tag', 'Gurux.AMI.SMTP/v4.1.2611.202')
        self.git('tag', 'Gurux.AMI.SMTP/v4.1.2611.209')
        self.commit('SMTP/file', 'next release')
        self.assertEqual(self.plan(), ['Gurux.AMI.SMTP/v4.1.2611.210'])

    def test_new_day_month_and_year_reset_sequence(self):
        self.git('tag', 'Gurux.AMI.SMTP/v4.1.2611.299')
        self.commit('SMTP/file', 'next release')
        for today, expected in [(date(2026, 11, 3), '4.1.2611.301'),
                                (date(2026, 12, 1), '4.1.2612.101'),
                                (date(2027, 1, 1), '4.1.2701.101')]:
            with self.subTest(today=today):
                self.assertEqual(self.plan(today=today), ['Gurux.AMI.SMTP/v' + expected])

    def test_daily_sequence_exhaustion_fails_without_changing_date(self):
        self.git('tag', 'Gurux.AMI.SMTP/v4.1.2611.299')
        self.commit('SMTP/file', 'next release')
        with self.assertRaisesRegex(ValueError, '99'):
            self.plan()

    def test_backward_date_fails_instead_of_downgrading(self):
        self.git('tag', 'Gurux.AMI.SMTP/v4.1.2611.301')
        self.commit('SMTP/file', 'next release')
        with self.assertRaisesRegex(ValueError, 'date'):
            self.plan()

    def test_retry_next_day_reuses_original_tag(self):
        self.commit('SMTP/file', 'release')
        self.git('tag', 'Gurux.AMI.SMTP/v4.1.2611.201')
        self.assertEqual(self.plan(today=date(2026, 11, 3)), ['Gurux.AMI.SMTP/v4.1.2611.201'])

    def test_shared_change_tags_both_modules(self):
        self.commit('shared.props', 'shared build fix')
        self.assertEqual(self.plan(), ['Gurux.AMI.OpenApi/v1.0.2611.201', 'Gurux.AMI.SMTP/v1.0.2611.201'])

    def test_unrelated_commit_creates_no_tags(self):
        self.git('tag', 'Gurux.AMI.SMTP/v1.0.0')
        self.commit('README.md', 'documentation')
        self.assertEqual(self.plan(), [])

    def test_retry_reuses_tag_on_same_commit(self):
        self.commit('SMTP/file', 'fix SMTP')
        self.git('tag', 'Gurux.AMI.SMTP/v1.0.0')
        self.assertEqual(self.plan(), ['Gurux.AMI.SMTP/v1.0.0'])

    def test_multi_commit_push_includes_earlier_module_change(self):
        self.commit('SMTP/file', 'fix SMTP')
        self.commit('README.md', 'documentation')
        self.assertEqual(self.plan(), ['Gurux.AMI.SMTP/v1.0.2611.201'])

    def test_new_branch_push_uses_full_history(self):
        self.assertEqual(self.plan('0' * 40), ['Gurux.AMI.SMTP/v1.0.2611.201'])

    def test_workflow_fix_push_recovers_previously_untagged_changes(self):
        self.git('tag', 'Gurux.AMI.SMTP/v4.1.2610.3001')
        self.commit('SMTP/file', 'previous push not released')
        before = self.git('rev-parse', 'HEAD')
        self.commit('.github/workflows/tag-modules.yml', 'fix workflow')
        self.assertEqual(self.plan(before), ['Gurux.AMI.SMTP/v4.1.2611.201'])

    def test_documentation_after_tag_does_not_release_module_again(self):
        self.git('tag', 'Gurux.AMI.SMTP/v4.1.2610.3001')
        self.commit('README.md', 'documentation')
        self.assertEqual(self.plan(), [])

    def test_first_release_includes_module_added_before_current_push(self):
        self.commit('OpenApi/file', 'add OpenApi in earlier push')
        self.git('tag', 'Gurux.AMI.SMTP/v4.1.2610.3002')
        before = self.git('rev-parse', 'HEAD')
        self.commit('.github/workflows/tag-modules.yml', 'fix workflow')
        self.assertEqual(self.plan(before), ['Gurux.AMI.OpenApi/v1.0.2611.201'])


if __name__ == '__main__':
    unittest.main()
