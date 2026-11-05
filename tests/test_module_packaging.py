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

"""Verify shared assembly filtering without an AMI host source tree."""
from pathlib import Path
import json
import shutil
import subprocess
import tempfile
import unittest
import zipfile
from xml.sax.saxutils import escape

TARGETS = Path(__file__).resolve().parents[1] / 'Directory.Build.targets'


@unittest.skipUnless(shutil.which('dotnet'), '.NET SDK required')
class ModulePackagingTests(unittest.TestCase):
    def test_all_registered_modules_use_release_version_and_enable_metadata(self):
        modules = json.loads((TARGETS.parent / 'modules.json').read_text(encoding='utf-8'))['modules']
        for identifier, module in modules.items():
            with self.subTest(module=identifier):
                result = subprocess.run([
                    'dotnet', 'msbuild', str(TARGETS.parent / module['project']), '-nologo',
                    '-p:Configuration=Release', '-p:AmiModuleReleaseVersion=1.0.2611.201',
                    '-getProperty:Version,PackageId,AmiModuleCompatibilityMetadata'],
                    capture_output=True, text=True)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                properties = json.loads(result.stdout)['Properties']
                self.assertEqual(properties['Version'], '1.0.2611.201')
                self.assertEqual(properties['PackageId'], identifier)
                self.assertEqual(properties['AmiModuleCompatibilityMetadata'].lower(), 'true')

    def test_all_registered_archives_use_release_version_and_module_name(self):
        modules = json.loads((TARGETS.parent / 'modules.json').read_text(encoding='utf-8'))['modules']
        for identifier, module in modules.items():
            with self.subTest(module=identifier):
                self.check_archive(identifier, TARGETS.parent / module['project'])

    def check_archive(self, identifier, project):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            publish = root / 'publish'
            publish.mkdir()
            (publish / 'module.txt').write_text('published module')
            result = subprocess.run([
                'dotnet', 'msbuild', str(project), '-t:ZipPublish', '-nologo',
                '-p:Configuration=Release', '-p:AmiModuleReleaseVersion=1.0.0',
                '-p:AmiModuleCompatibilityMetadata=false', '-p:AmiModuleExcludeHostAssemblies=false',
                f'-p:PublishDir={publish.as_posix()}/',
                f'-p:ProjectDir={root.as_posix()}/',
                f'-p:AmiModuleArchiveDirectory={root.as_posix()}/bin/Release'],
                cwd=root, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            archive = root / f'bin/Release/{identifier}-1.0.0.zip'
            self.assertTrue(archive.is_file(), result.stdout + result.stderr)
            with zipfile.ZipFile(archive) as package:
                self.assertEqual(package.read('module.txt'), b'published module')

    def test_filters_shared_contracts_and_stale_satellites_but_keeps_private_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            publish = root / 'publish'
            (publish / 'fi').mkdir(parents=True)
            names = ['Example.Module.dll', 'Private.Library.dll', 'Private.Library.xml',
                     'Gurux.DLMS.AMI.Module.dll', 'Gurux.DLMS.AMI.Module.pdb',
                     'Microsoft.Extensions.Options.dll', 'System.Text.Json.dll',
                     'Gurux.UI.Components.xml', 'fi/Gurux.UI.Components.resources.dll', 'module.json',
                     'Microsoft.Private.Dependency.dll', 'System.Private.Dependency.dll']
            for name in names:
                (publish / name).write_text('fixture')
            references = ['Gurux.DLMS.AMI.Module', 'Gurux.UI.Components',
                          'Microsoft.Extensions.Options', 'System.Text.Json', 'Private.Library',
                          'Microsoft.Private.Dependency', 'System.Private.Dependency']
            project = root / 'verify.proj'
            project.write_text('''<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <PublishDir>''' + escape(publish.as_posix()) + '''/</PublishDir>
  </PropertyGroup>
  <ItemGroup>
''' + ''.join(f'    <ReferencePath Include="refs/{name}.dll"' +
                (' FrameworkReferenceName="Microsoft.NETCore.App"' if name in
                 ('Microsoft.Extensions.Options', 'System.Text.Json') else '') + ' />\n' for name in references) +
                ''.join(f'    <ResolvedFileToPublish Include="publish/{name}" />\n' for name in names) + '''
  </ItemGroup>
  <Target Name="ResolveReferences" />
  <Import Project="''' + escape(TARGETS.as_posix()) + '''" />
  <Target Name="Verify" DependsOnTargets="AmiFilterModulePublishFiles;AmiRemoveStaleHostPublishFiles">
    <WriteLinesToFile File="report.txt" Lines="@(ResolvedFileToPublish)" Overwrite="true" />
  </Target>
</Project>''', encoding='utf-8')
            result = subprocess.run(['dotnet', 'msbuild', str(project), '-t:Verify', '-nologo'],
                                    cwd=root, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            kept = (root / 'report.txt').read_text().replace('\\', '/').splitlines()
            expected = ['Example.Module.dll', 'Private.Library.dll', 'Private.Library.xml', 'module.json',
                        'Microsoft.Private.Dependency.dll', 'System.Private.Dependency.dll']
            self.assertCountEqual(kept, ['publish/' + name for name in expected])
            self.assertCountEqual([str(p.relative_to(publish)).replace('\\', '/')
                                   for p in publish.rglob('*') if p.is_file()], expected)
