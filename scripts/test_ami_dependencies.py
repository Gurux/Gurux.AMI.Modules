"""Offline behavioral tests; external NuGet and dotnet calls are substituted."""
import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('ami_dependencies', Path(__file__).with_name('ami_dependencies.py'))
ami = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ami)
MODULE = 'Gurux.DLMS.AMI.Module'
SHARED = 'Gurux.DLMS.AMI.Shared'


class DependencyTests(unittest.TestCase):
    def test_four_part_versions_and_prerelease(self):
        self.assertGreater(ami.version_key('4.1.2611.101'), ami.version_key('4.1.2610.3001'))
        self.assertGreater(ami.version_key('1.0.0'), ami.version_key('1.0.0-rc.10'))
        self.assertGreater(ami.version_key('1.0.0-rc.10'), ami.version_key('1.0.0-rc.2'))
        with self.assertRaises(ValueError):
            ami.version_key('$(Version)')

    def test_retries_successful_but_stale_index(self):
        with patch.object(ami, 'fetch_versions', side_effect=[['1.0.0'], ['2.0.0-rc.1', '2.0.0']]) as fetch, patch.object(ami.time, 'sleep'):
            self.assertEqual(ami.latest_stable(MODULE, '2.0.0', attempts=2), '2.0.0')
            self.assertEqual(fetch.call_count, 2)

    def test_exhausted_propagation_is_failure(self):
        with patch.object(ami, 'fetch_versions', return_value=['1.0.0']), patch.object(ami.time, 'sleep'):
            with self.assertRaises(RuntimeError):
                ami.latest_stable(MODULE, '2.0.0', attempts=2)

    def test_selects_highest_stable_unsorted(self):
        with patch.object(ami, 'fetch_versions', return_value=['4.0.0', '10.0.0-rc.1', '2.0.0']):
            self.assertEqual(ami.latest_stable(MODULE), '4.0.0')

    def test_transient_network_failure_is_retried(self):
        with patch.object(ami, 'fetch_versions', side_effect=[OSError('offline'), ['2.0.0']]), patch.object(ami.time, 'sleep'):
            self.assertEqual(ami.latest_stable(MODULE, attempts=2), '2.0.0')

    def test_comments_and_unconsumed_packages_remain_unchanged(self):
        text = f'<!-- <PackageReference Include="{MODULE}" Version="0.0.1"/> --><Project/>'
        self.assertEqual(ami.update_xml(text, {MODULE: '2.0.0'}), (text, []))

    def test_version_override_child_and_unsupported_expression(self):
        text = f'<PackageReference Include="{MODULE}"><VersionOverride>1.0.0</VersionOverride></PackageReference>'
        self.assertIn('<VersionOverride>2.0.0</VersionOverride>', ami.update_xml(text, {MODULE: '2.0.0'})[0])
        with self.assertRaises(ValueError):
            ami.update_xml(f'<PackageReference Include="{MODULE}" Version="$(AmiVersion)"/>', {MODULE: '2.0.0'})

    def test_updates_individually_without_downgrade_or_unrelated_edits(self):
        text = f'''<Project><Version>7.0.0</Version><ItemGroup>
<PackageReference Version='1.0.0' Include='{MODULE}' />
<PackageReference Include="{MODULE}"><Version>3.0.0</Version></PackageReference>
<PackageReference Include="Other" Version="1.0.0" />
</ItemGroup></Project>'''
        updated, changes = ami.update_xml(text, {MODULE: '2.0.0'})
        self.assertIn("Version='2.0.0'", updated)
        self.assertIn('<Version>3.0.0</Version>', updated)
        self.assertIn('<Version>7.0.0</Version>', updated)
        self.assertIn('Include="Other" Version="1.0.0"', updated)
        self.assertEqual(changes, [(MODULE, '1.0.0', '2.0.0')])
        self.assertEqual(ami.update_xml(updated, {MODULE: '2.0.0'})[1], [])

    def test_central_versions_only_for_existing_consumers(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp).resolve()
            (root / 'Directory.Packages.props').write_text(f'<Project><ItemGroup><PackageVersion Include="{MODULE}" Version="1.0.0"/><PackageVersion Include="{SHARED}" Version="1.0.0"/></ItemGroup></Project>')
            (root / 'A.csproj').write_text(f'<Project><ItemGroup><PackageReference Include="{MODULE}"/></ItemGroup></Project>')
            nested = root / 'nested'
            nested.mkdir()
            (nested / 'Directory.Packages.props').write_text(f'<Project><ItemGroup><PackageVersion Include="{MODULE}" Version="3.0.0"/></ItemGroup></Project>')
            (nested / 'B.csproj').write_text(f'<Project><ItemGroup><PackageReference Include="{MODULE}"/></ItemGroup></Project>')
            graph = ami.discover(root)
            changes, files, seeds = ami.apply_updates(root, graph, {MODULE: '2.0.0', SHARED: '2.0.0'})
            self.assertEqual(len(changes), 1)
            self.assertEqual(seeds, {root / 'A.csproj'})
            self.assertIn(f'Include="{SHARED}" Version="1.0.0"', (root / 'Directory.Packages.props').read_text())
            self.assertIn('Version="3.0.0"', (nested / 'Directory.Packages.props').read_text())

    def test_transitive_dependents_and_test_sdk_detection(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp).resolve()
            for name, body in {
                'A': f'<PackageReference Include="{MODULE}" Version="1.0.0"/>',
                'B': '<ProjectReference Include="A.csproj"/>',
                'C.Tests': '<ProjectReference Include="B.csproj"/><PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0"/>',
                'Other': '',
            }.items():
                (root / f'{name}.csproj').write_text(f'<Project><ItemGroup>{body}</ItemGroup></Project>')
            graph = ami.discover(root)
            affected = ami.dependents(graph, {root / 'A.csproj'})
            self.assertEqual({p.stem for p in affected}, {'A', 'B', 'C.Tests'})
            self.assertTrue(graph[root / 'C.Tests.csproj']['test'])

    def test_discover_normalizes_repository_root(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp).resolve()
            nested = root / 'nested'
            nested.mkdir()
            (root / 'A.csproj').write_text('<Project/>')
            (root / 'B.csproj').write_text('<Project><ItemGroup><ProjectReference Include="A.csproj"/></ItemGroup></Project>')

            graph = ami.discover(nested / '..')

            self.assertEqual(set(graph), {root / 'A.csproj', root / 'B.csproj'})
            self.assertEqual(ami.dependents(graph, {root / 'A.csproj'}),
                             {root / 'A.csproj', root / 'B.csproj'})

    def test_discover_rejects_reference_outside_repository(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp).resolve() / 'repo'
            root.mkdir()
            (root.parent / 'Outside.csproj').write_text('<Project/>')
            (root / 'A.csproj').write_text('<Project><ItemGroup><ProjectReference Include="../Outside.csproj"/></ItemGroup></Project>')

            with self.assertRaisesRegex(ValueError, 'Project reference outside repository'):
                ami.discover(root)

    def test_validate_is_offline_from_latest_version_lookup_and_requires_locks(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp).resolve()
            project = root / 'A.csproj'
            project.write_text(f'<Project><ItemGroup><PackageReference Include="{MODULE}" Version="1.0.0"/></ItemGroup></Project>')
            with patch.object(ami, 'fetch_versions', side_effect=AssertionError('must not query latest')), patch.object(ami, 'dotnet') as dotnet:
                graph = ami.discover(root)
                with self.assertRaisesRegex(RuntimeError, 'Missing lock file'):
                    ami.validate(graph, {project})
                dotnet.assert_not_called()
                (root / 'packages.lock.json').write_text('{}')
                ami.validate(graph, {project})
                self.assertEqual(dotnet.call_args_list[0].args, ('restore', project, '--locked-mode', '--use-lock-file'))
                self.assertEqual(dotnet.call_count, 2)

    def test_unknown_central_version_fails_instead_of_silently_skipping(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp).resolve()
            (root / 'Directory.Packages.props').write_text('<Project/>')
            (root / 'A.csproj').write_text(f'<Project><ItemGroup><PackageReference Include="{MODULE}"/></ItemGroup></Project>')
            with self.assertRaisesRegex(ValueError, 'No literal'):
                ami.apply_updates(root, ami.discover(root), {MODULE: '2.0.0'})


if __name__ == '__main__':
    unittest.main()
