[bari](http://vigoo.github.io/bari/)
====
 
[Bari](http://vigoo.github.io/bari/) is an advanced build management tool for .NET projects.

![CI](https://github.com/vigoo/bari/workflows/CI/badge.svg)
[![Apache 2 License License](http://img.shields.io/badge/license-APACHE2-blue.svg)](http://www.apache.org/licenses/LICENSE-2.0)

# Getting started #
## Getting bari ##

Bari itself targets **.NET 10** and remains a Bari project: `suite.yaml` is the
source of truth; the solution and project files are generated.

On Windows, install the .NET 10 SDK, PowerShell 7 and Git, then run:

```powershell
./bootstrap.ps1 -Test
```

This builds a pinned legacy seed when needed, generates the projects using Bari,
builds the .NET 10 host, and uses that host to rebuild Bari and run the tests.
The result is in `target/full`; keep the whole directory, including `lib` and
`runtimes`. No Visual Studio installation or globally installed Bari is required.
The initial seed build needs a full Git history and internet access to NuGet.

To use an existing Bari from this fork as the seed (it must understand .NET 10):

```powershell
./bootstrap.ps1 -BariPath C:/Bari/bari.exe -Test
```

For subsequent changes, rerun bootstrap with an existing .NET 10 distribution,
or invoke its `bari.dll` from outside this checkout's `target` directory:

```powershell
dotnet C:/Bari-net10/bari.dll --target release build full
dotnet C:/Bari-net10/bari.dll --target release test
```

This build uses **IronPython 3**. Python 2 scripts and old compiled plugins need
migration before use. Legacy NuGet distributions and `selfupdate` cannot update
this runtime. See [the migration notes](doc/net10-migration.md) for details.

## Documentation ##
Documentation is under construction and available from the [getting started page](https://github.com/vigoo/bari/wiki/GettingStarted).

Additionally I recommend browsing the following suite definitions for examples:

* [Simple C# executable](https://github.com/vigoo/bari/blob/master/systest/single-cs-exe/suite.yaml)
* [Simple F# executable](https://github.com/vigoo/bari/blob/master/systest/single-fs-exe/suite.yaml)
* [Simple C++ executable](https://github.com/vigoo/bari/tree/master/systest/single-cpp-exe)
* [Dependencies within a module](https://github.com/vigoo/bari/blob/master/systest/module-ref-test/suite.yaml)
* [Dependencies within a suite](https://github.com/vigoo/bari/blob/master/systest/suite-ref-test/suite.yaml)
* [Content files support](https://github.com/vigoo/bari/blob/master/systest/content-test/suite.yaml)
* [Support for reference aliases](https://github.com/vigoo/bari/blob/master/systest/alias-test/suite.yaml)
* [File system repository](https://github.com/vigoo/bari/blob/master/systest/fsrepo-test/suite.yaml) 
* [C++ static library support](https://github.com/vigoo/bari/blob/master/systest/static-lib-test/suite.yaml)
* [C++ resource support](https://github.com/vigoo/bari/blob/master/systest/cpp-rc-support/suite.yaml)
* [C++/CLI support](https://github.com/vigoo/bari/blob/master/systest/mixed-cpp-cli/suite.yaml)
* [Registration-free COM support](https://github.com/vigoo/bari/blob/master/systest/regfree-com-server/suite.yaml)
* [Postprocessor scripts](https://github.com/vigoo/bari/blob/master/systest/postprocessor-script-test/suite.yaml)
* [Custom plugin support](https://github.com/vigoo/bari/blob/master/systest/custom-plugin-test/suite.yaml)

And the [Bari suite definition](https://github.com/vigoo/bari/blob/master/suite.yaml) itself! 

To get a list of available commands, use
`bari help`.

## Visual Studio add-on

A Visual Studio add-on for bari [is also in development](https://github.com/zvrana/bari-vs-addon). 
