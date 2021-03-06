# OneDAS-Core

[![AppVeyor](https://ci.appveyor.com/api/projects/status/github/onedas-group/onedas-core?svg=true)](https://ci.appveyor.com/project/Apollo3zehn/onedas-core) [![Documentation Status](https://readthedocs.org/projects/onedas/badge/?version=latest)](http://onedas.readthedocs.io/en/latest/?badge=latest)
[![NuGet](https://img.shields.io/nuget/vpre/OneDAS.Core.svg)](https://www.nuget.org/packages?q=OneDAS) [![License](https://img.shields.io/github/license/OneDAS-Group/OneDAS-Core.svg)](https://github.com/OneDAS-Group/OneDAS-Core/blob/master/LICENSE.md)

Contains the main executables and type definitions of OneDAS.

The documentation is hosted on [onedas.readthedocs.io](https://onedas.readthedocs.io) but incomplete and written for the old version of OneDAS. It will be updated continuously in the following weeks and months.

### Prerequisites

* Visual Studio 2017 Community
* Git for Windows (https://git-scm.com/download/win)
* node.js (https://nodejs.org/en/)
* **optional**: WiX Toolset (to open .wixproj and to build .msi packages)
  * WiX Toolset build tools v3.11 (http://wixtoolset.org/releases/)
  * WiX Toolset Visual Studio 2017 Extension (http://wixtoolset.org/releases/)"

### Initialization

Run the following Powershell script to get started with OneDAS:

```
# constants
$projectName = "OneDAS"
$origin = "https://github.com/OneDAS-Group/"

# create parent folder
md -Force  $projectName | Out-Null
cd $projectName
$rootFolder = (Get-Location).Path

# clone projects
git clone "$origin/$projectName-Core" --quiet
git clone "$origin/$projectName-Documentation" --quiet

# initial solution
cd $rootFolder/$projectName-Core
./init_solution.ps1
```
