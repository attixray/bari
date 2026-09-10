# Bari on .NET 10

The executable and all bundled plugins target `net10.0`. `suite.yaml` owns their
framework, package versions and build configuration. Generated `.csproj` and
`.sln` files remain untracked. `global.json` selects stable .NET 10 SDKs and allows
newer feature bands within .NET 10.

## Build and verify

Run `./bootstrap.ps1 -Test` from a full Git checkout on Windows with PowerShell 7
and the .NET 10 SDK. The script downloads Bari 1.0.3.25 and the .NET Framework 4.0
reference assemblies, builds the last Framework revision
`9386ad006f32f4121428f57e0732f6a5964ea18b` in `_bootstrap`, then uses it to generate
this suite. This bridge is necessary because published Bari 1.0.3.25 cannot parse
.NET 10 projects. The old source and dependencies are confined to the seed build;
the resulting distribution uses the packages listed below.

`-BariPath` skips building that bridge and uses a supplied Bari distribution from
this fork. Both an executable and `bari.dll` are accepted. The host is copied to
an isolated directory before cleaning `target`, so rebuilding cannot overwrite
the running Bari. Use `-Configuration debug` for a debug final build.

The bootstrap generates the solution with `bari --target bootstrap vs full`,
compiles it with the .NET SDK, then runs `bari --target release rebuild full`
with the new .NET 10 host. The final `target/full` distribution is started as a
separate process. `-Test` also runs the unit tests through `bari test` and
`scripts/test-net10.ps1`: an SDK project, a project reference, a NuGet dependency,
a Python postprocessor, an incremental build and tests in a path containing
spaces. The smoke script also validates repository Python syntax and tests
`HG_REVNO` when Mercurial is installed.

For an SDK-only downstream suite, select the new runner explicitly:

```yaml
msbuild:
    version: Dotnet
    restore: true
```

Existing Visual Studio/MSBuild selections remain available for Framework,
C++ and C++/CLI suites. The .NET SDK runner cannot replace Visual Studio's native
C++ toolchain. Initial bootstrapping and CI are currently verified on Windows;
other platforms need an existing .NET 10 Bari seed and separate validation.

## Dependencies

| Dependency | Version / replacement |
| --- | --- |
| Ninject | 3.3.6 |
| Ninject.Extensions.Factory | 3.3.3 |
| Castle.Core | 5.2.1 |
| log4net | 3.4.0 |
| YamlDotNet | 18.1.0, replaces the two bundled legacy DLLs |
| QuickGraph | Replaced by QuikGraph 2.5.0 |
| IronPython and standard library | 3.4.2 |
| DynamicLanguageRuntime | 1.3.5 |
| System.Drawing.Common | 10.0.11, explicitly upgrades the scripting dependency |
| Newtonsoft.Json | 13.0.4 |
| Visual Studio Setup interop | 3.14.2075 |
| NUnit / adapter / test SDK | 4.6.1 / 6.3.0 / 18.10.0 |
| Moq | 4.20.72 |
| FluentAssertions | 7.2.0; retained on the Apache 2.0 line |
| DotNetZip | Removed; uses `System.IO.Compression` |
| Monads | Removed; uses C# null handling and iteration |
| Mercurial.Net | Removed; invokes `hg` directly |
| NuSelfUpdate / NuGet.Core | Removed from the runtime |
| Microsoft.Build.Utilities.Core | Removed unused reference |

PackageReference restore resolves transitive dependencies. The external NuGet
command remains available for legacy suites and packaging. The migration does
not turn Bari into a `dotnet tool` package.

## Compatibility changes

- **Python 3:** convert `print x` to `print(x)`, old exception syntax, `xrange`,
  `iteritems`, and any dependency on Python 2 integer division or comprehension
  variable leakage. The checked-in scripts have been migrated. IronPython
  implements Python 3.4 semantics, so scripts should avoid newer Python syntax.
  Keeping IronPython 2.7.12 failed even when importing `json` on .NET 10; see the
  [upstream runtime issue](https://github.com/IronLanguages/ironpython2/issues/848).
- **Plugins:** rebuild custom plugins against the new Bari.Core and package
  versions. The public graph types now come from QuikGraph; YAML and Ninject
  references also changed. Plugins share Bari's assembly context. Their dependency
  manifests and managed/native runtime assets must be deployed together.
- **Deployment:** copy all of `target/full`, including the apphost, `bari.dll`,
  `.deps.json`, `.runtimeconfig.json`, `lib/`, and `runtimes/`. The build is
  framework-dependent and requires the .NET 10 runtime. Recursive build outputs
  now preserve these directories during product merging and cache restoration.
- **Updates:** `selfupdate` returns a clear error instead of replacing the .NET 10
  distribution with an old Framework NuGet package. Update with bootstrap or by
  replacing the complete distribution.
- **Contracts:** runtime Code Contracts rewriting is disabled for Bari itself.
  The Code Contracts plugin remains available to existing Framework suites.
- **Caches:** bootstrap performs a clean rebuild. Do not share build caches
  between the Framework and .NET 10 Bari distributions.

Performance depends on the workload. Startup, YAML parsing, dependency graph
work, external compilers and cache I/O should be measured separately; a runtime
upgrade alone does not establish an end-to-end build speedup.

## Local validation (2026-09-10)

On Windows x64 with SDK 10.0.400 / runtime 10.0.11, the unit suite passed 323 tests.
The runtime smoke test also passed, including its separate test assembly,
Python 3 standard-library imports, all four checked-in Python scripts, and
Mercurial revision substitution. NuGet's transitive vulnerability audit reported
no vulnerable packages in the 19 projects of the full distribution.

The legacy `single-cs-exe` fixture built successfully. Its old `v4.5-client`
target uses a GAC fallback on this machine because that reference pack is absent;
this does not validate every legacy target or the C++/CLI toolchain.

A small startup/suite-parsing comparison ran `bari --target bootstrap info`
against this suite, alternating the saved Framework build and the .NET 10 build.
After one warm-up per host, seven runs each gave medians of **597.9 ms** and
**535.5 ms** respectively (about **10% less elapsed time**). This compares the
whole migrated distribution, including dependency upgrades, on one machine with
warm filesystem caches. Full application-build performance has not been measured.
