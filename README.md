# Gurux DLMS AMI Modules

Modules and extensions for Gurux DLMS AMI, with automated releases and module-specific changelogs.

## Releases

Each module has independent versions and GitHub Releases. Pushing commits to `main` automatically creates a tag for each module whose `changelogPaths` changed, then starts its release workflow. Changes are compared with the latest stable module tag so a later push also picks up changes missed by a failed tagging run. For modules without a stable tag, all tracked sources are considered unreleased, including sources added in earlier pushes. Shared source changes can release several modules; unrelated changes create no tags when there are no pending module changes.

Automatic tags increment the last numeric component of the highest reachable stable module tag (for example, `Gurux.AMI.SMTP/v4.1.2610.3001` becomes `Gurux.AMI.SMTP/v4.1.2610.3002`). A module without a stable tag starts at `1.0.0`. Rerunning the tagging job reuses tags already created for the same commit. The workflow uses `GITHUB_TOKEN` with Contents and Actions write permissions to create tags and explicitly dispatch releases; it needs no additional token. ZIP packages and commit-based changelogs are also published when a module tag is pushed manually.

For example, to publish SMTP version 1.2.0, commit the changes and push the tag:

```shell
git tag Gurux.AMI.SMTP/v1.2.0
git push origin Gurux.AMI.SMTP/v1.2.0
```

Use a suffix such as `-rc1` for prereleases. After publishing, the workflow notifies [Gurux AMI Catalog](https://github.com/Gurux/Gurux.AMI.Catalog), which updates its catalog and RSS feed.

## Build and GitHub Actions

Releases run on GitHub's `windows-latest` runner. The workflow installs .NET 10 and Python automatically. SMTP restores all dependencies from NuGet; no local AMI or UI source trees or self-hosted runner are needed.

Commit `NuGet.Config` and the module's `packages.lock.json` along with the project files. Releases use locked NuGet restore so dependency changes must be committed before tagging.

To build SMTP locally with the .NET 10 SDK:

```shell
dotnet restore Gurux.AMI.SMTP/Gurux.AMI.SMTP.csproj --locked-mode
dotnet publish Gurux.AMI.SMTP/Gurux.AMI.SMTP.csproj --configuration Release --no-restore -p:AmiModuleReleaseVersion=1.2.0
```

The ZIP is created in `Gurux.AMI.SMTP/bin/Release/`. Shared host assemblies are excluded from the package. To update package versions, edit the project, run `dotnet restore --force-evaluate` for that project and commit the updated lock file.

Add repository secret `CATALOG_DISPATCH_TOKEN`: a fine-grained PAT scoped to `Gurux/Gurux.AMI.Catalog`, with Contents read/write permission. Catalog must have its matching workflow and GitHub Pages enabled. The former `AMI_SOURCE_ROOT` and `UI_COMPONENTS_SOURCE_ROOT` variables are no longer used.

To retry a release or Catalog notification, run **Actions → Release AMI module → Run workflow** with the existing tag. Published ZIP packages are not overwritten.

Automatic module tags use `major.minor.YYMM.DDNN` based on the UTC publication
date. The existing major/minor is preserved (initially `1.0` for a new module).
For example, the first release on November 2, 2026 is `4.1.2611.201`, followed
by `4.1.2611.202`; November 3 starts at `4.1.2611.301`. The final two digits
are the day's sequence, from `01` through `99`. Tagging fails if the date would
downgrade the latest version or if the day's sequence is exhausted. Retrying
the same commit reuses its existing tag, even on a later day.

## Package compatibility

Publish builds generate `module.compatibility.json` inside the ZIP. The workflow uploads a matching `<id>-<version>.compatibility.json` release asset before publishing the draft. Catalog validates this sidecar and copies its exact host assembly requirements to the release's `references` field, used by AMI's existing pre-install compatibility check.

For additional modules, set `AmiModuleCompatibilityMetadata` to `true` in the project, keep its assembly name equal to its module ID, and set `compatibilityMetadata: true` on its Catalog source. Include the `scripts/Gurux.AMI.Module.Metadata/` tool when uploading the repository. Missing or mismatched metadata prevents publication or Catalog replacement.

Run the checks locally:

```shell
python -m unittest discover -s tests -v
dotnet test tests/Smtp.Tests/Smtp.Tests.csproj --configuration Release -p:RestoreLockedMode=true
dotnet test tests/ModuleMetadata.Tests/ModuleMetadata.Tests.csproj --configuration Release
```

## Automated AMI dependency updates

[Update AMI dependencies](.github/workflows/update-ami-dependencies.yml) runs daily at 02:23 UTC, manually, and on `repository_dispatch` event `ami-dependency-published`. Merge this workflow into the default branch to enable scheduling and dispatch. Manual update runs must select the default branch; other branches are skipped.

The updater checks both `Gurux.DLMS.AMI.Shared` and `Gurux.DLMS.AMI.Module` on NuGet.org, ignores prereleases, and only raises existing literal package versions. Each reference is compared independently, so a higher version is never downgraded and lower references still catch up. No Shared reference is added when Shared is only transitive. NuGet queries retry up to five times, 20 seconds apart (30 second request timeout). A stable dispatch version is a minimum visibility hint: if it is still absent after the retries, the run fails without pushing; rerun later or let the daily check retry.

Existing `PackageReference` versions (attributes or child elements, including `VersionOverride`) and the nearest `Directory.Packages.props` `PackageVersion` entries are supported. Central entries are updated only when a project actually consumes that package. Version properties, ranges and floating versions require a manual update rather than a guessed replacement. Project references must remain inside the repository. Discovery uses Git's tracked and nonignored project files; local ignored `Development/`, `bin/` and `obj/` projects are excluded.

Changed consumers, all reverse project-reference dependents, and their related test projects are verified. Test projects are identified by `IsTestProject` or `Microsoft.NET.Test.Sdk`. Lock files for the entire affected project graph are refreshed with `dotnet restore --force-evaluate --use-lock-file`. Verification then requires committed locks and runs `dotnet restore --locked-mode --use-lock-file`, Release builds with package generation disabled, and related tests. The read-only PR check conservatively verifies all AMI consumers and their dependents, including tests, without looking up newer NuGet versions.

The sibling `Gurux.SMTP.csproj` uses `obj/Gurux.SMTP/` and `packages.Gurux.SMTP.lock.json` to avoid sharing restore assets and locks with `Gurux.AMI.SMTP.csproj`. Its required Http/Services references and root namespace match the shared source code. The release module retains its existing paths.

### Repository settings and credentials

Enable GitHub Actions and **Settings → Actions → General → Workflow permissions → Allow GitHub Actions to create and approve pull requests** (organization policy must permit it). This workflow creates PRs; it does not approve them. Its update job requests `contents: write` and `pull-requests: write`; the PR validation job has only `contents: read`, no write secrets, and no persisted checkout credentials. No additional token is needed for dependency updates in this repository.

Updates use one bot-owned branch, `chore/ami-dependency-updates`, and one open PR. Do not put manual commits on this branch: the updater regenerates it from the default branch and pushes with `--force-with-lease`. Identical updates reuse the existing commit; the PR body includes every old/new version, affected module paths, tests and a link to the validation run. No PR is created when versions do not change. Build/test failures prevent pushing.

Do not rely on events from `GITHUB_TOKEN` to automatically run a second workflow. The trusted default-branch update run executes all compatibility checks **before** pushing or creating/updating its PR. Review that linked run for bot PRs. Ordinary PR events run the independent read-only check. If branch protection requires that PR check and the bot event is suppressed or awaits approval, approve the check run where available or use a repository-scoped GitHub App token for the PR creation/push step (`contents: write`, `pull-requests: write`). The built-in updater verification is not a separate status on the PR commit.

### Manual and local use

Open **Actions → Update AMI dependencies → Run workflow**, select the default branch, and inspect the resulting run and PR. .NET 10, Python 3.12 and PowerShell 7 are installed by the workflow. Locally, with those tools installed:

```powershell
# Verify current dependencies without changing version declarations or locks:
./scripts/update_ami_dependencies.ps1 -Mode Validate
# Query NuGet, update existing references, refresh locks, build and test:
./scripts/update_ami_dependencies.ps1 -Mode Update
# After a manual dependency change or adding a related project, regenerate locks:
./scripts/update_ami_dependencies.ps1 -Mode Lock
python scripts/test_ami_dependencies.py
```

Commit all affected lock files, including related test-project locks. `Lock` also runs verification. The script itself never commits, pushes or opens PRs; those steps belong to the workflow.

### Release after merging

Merge the dependency PR, then publish each affected module separately with a **new** module tag, for example `Gurux.AMI.SMTP/v1.2.1`. The dependency update never changes module versions, creates tags, runs `dotnet publish`, publishes NuGet packages or uploads/replaces ZIP assets. The existing `release-module.yml` process remains unchanged; already published ZIPs are retained.

### Optional upstream notification from Gurux/Gurux.DLMS.AMI4

The upstream `publish-shared.yml` can send the following event after a successful Shared or Module NuGet push:

```json
{
  "event_type": "ami-dependency-published",
  "client_payload": {
    "package": "Gurux.DLMS.AMI.Module",
    "version": "4.1.2611.101"
  }
}
```

Configure **in AMI4** secret `AMI_MODULES_DISPATCH_TOKEN` with a fine-grained PAT restricted to the target modules repository and **Contents: read and write**, or a short-lived GitHub App installation token restricted to that repository with the same permission. A long-lived PAT may be stored as the secret; an App token should instead be minted at run time and supplied to the notification step. AMI4's ordinary `GITHUB_TOKEN` cannot dispatch to another repository.

The default notification target is `Gurux/Gurux.DLMS.AMI.Modules`; set AMI4 Actions variable `AMI_MODULES_REPOSITORY` if the repository has a different name (for example this checkout's current origin, `Gurux/Gurux.AMI.Modules`). The step builds structured JSON, runs only for Shared/Module, skips a missing token and has `continue-on-error: true`. Notification failure does not fail NuGet publication; daily polling is the fallback. No upstream publication is needed to test the updater: use its manual run.


## Independent module localization and assets

The existing module release remains `Gurux.AMI.SMTP/v1.0.5`. Independent content uses `Gurux.AMI.SMTP/localization/v1.0.2` or `Gurux.AMI.SMTP/assets/v1.0.3`. These are examples only: nothing releases until an existing tag is pushed or the corresponding workflow is manually run with an existing tag. No tags are created by the workflows.

Every entry in `modules.json` can configure `localization` and `assets` separately. Each requires a dedicated `directory` inside its project directory and an explicit `ownerVersionRange`, for example `[1.0.0,2.0.0)`. Localization also requires `defaultCulture`, for example `en`. This range describes compatible installed module versions. It is a manual compatibility decision; it is not inferred from the independently versioned content package. Deployment must intentionally apply module owner compatibility ranges.

SMTP's English JSON is copied faithfully from `Properties/Resources.resx`; its asset source contains the distributable `Smtp.svg`. Culture JSON files are bundled together in one localization JSON package. Assets are bundled in one resource ZIP. Source code, assemblies, build output, hidden paths, secrets, symlinks, and sources outside the configured module directory are rejected. The resource workflow builds only the Updater packaging tool and never builds a module DLL.

Configure the existing `CATALOG_DISPATCH_TOKEN` repository secret with Contents: write on `Gurux/Gurux.AMI.Catalog`; the workflow's GitHub token publishes releases in this repository. The workflow must also be able to read `Gurux/Gurux.Updater.Net`. Localization and assets dispatch `localization-release` and `resources-release` respectively, with module, packageType, repository, and tag. A notification failure can be retried with the same tag. Published content releases are immutable: a retry downloads and verifies their existing manifest and package instead of rebuilding or replacing assets. Draft releases can be retried before publication.

The configured SMTP layout is:

```text
Gurux.AMI.SMTP/
  Gurux.AMI.SMTP.csproj
  localized/
    en.json
    fi.json                 # add when a Finnish translation is available
  asset/
    Smtp.svg
    images/                 # optional subdirectories retain their relative paths
```

`fi.json` above is an example, not an existing translation. Every `localized/<culture>.json` is a flat string-key/string-value JSON object. Duplicate keys, invalid culture names, a missing default culture, and empty language maps fail the shared Updater validator. Keep only distributable public files in these registered directories; extension/name checks are not a substitute for reviewing content for secrets.

Example release commands, after committing the content and its compatibility configuration:

```shell
git tag Gurux.AMI.SMTP/v1.0.5
git push origin Gurux.AMI.SMTP/v1.0.5
# Independently, without changing the module's project version:
git tag Gurux.AMI.SMTP/localization/v1.0.2
git push origin Gurux.AMI.SMTP/localization/v1.0.2
git tag Gurux.AMI.SMTP/assets/v1.0.3
git push origin Gurux.AMI.SMTP/assets/v1.0.3
```

For a manual content release or a failed release/notification retry, open **Actions → Release AMI module content → Run workflow** on the default branch and provide the exact existing localization/assets tag. Checkout explicitly selects `refs/tags/<tag>`. A changed file alone does not release anything. Retry the same tag after transient failures; correct invalid sources/compatibility in a new commit and use a new tag. Published version contents are never replaced. Module releases continue to use **Release AMI module**.

Merge the supporting Updater and Catalog changes first: both workflows consume the Updater tool's current default branch. A short-lived App installation token or fine-grained PAT for `CATALOG_DISPATCH_TOKEN` must be scoped to Catalog, with Contents read/write; this repository's ordinary `GITHUB_TOKEN` cannot notify a different repository. Catalog's existing Pages settings and content source allowlist must include `Gurux/Gurux.AMI.Modules`. GitHub Releases need Contents write only in the publishing job.

Local verification (Python 3.12, PowerShell 7 and .NET 10), with no tags or releases:

```powershell
dotnet build ../Gurux.Updater.Net/Development/Gurux.Updater.Tool -c Release -p:GeneratePackageOnBuild=false
$env:GURUX_CONTENT_TOOL = (Resolve-Path ../Gurux.Updater.Net/Development/Gurux.Updater.Tool/bin/Release/net10.0/Gurux.Updater.Tool.dll).Path
python -m unittest discover -s tests -v
dotnet test ../Gurux.Updater.Net/Tests/ModuleContent.Tests -c Release -p:GeneratePackageOnBuild=false
```

The integration test builds real JSON/ZIP packages, imports them locally, lists and installs each type, and uses a deliberately unbuildable example module to prove that no module DLL is built or project version changed. No GitHub API is invoked by that test. Updater installation/check commands are documented in its README; `--install` is the existing module's directory and `--owner-version` is its actual installed version.

## License

This software is available under either of the following licenses:

- **Open-source license:** GNU General Public License, version 2 only
  (GPL-2.0-only). See [LICENSE](LICENSE) for the full license text.
- **Commercial license:** Available from Gurux Ltd under a separate
  written agreement. This option allows use under the terms of that
  agreement instead of GPL-2.0-only.

A valid commercial license is required to use the commercial licensing
option. See [Gurux Commercial License Notice](LICENSES/LicenseRef-Gurux-Commercial.txt).

For commercial licensing inquiries, [contact Gurux Ltd](https://www.gurux.fi/).
