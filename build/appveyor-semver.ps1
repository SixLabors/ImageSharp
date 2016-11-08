$version=[Version]$Env:APPVEYOR_BUILD_VERSION
$version_suffix=$Env:version_suffix

$basever=$version.Major.ToString() + "." + $version.Minor.ToString() + "." + $version.Build.ToString()

$semver = $basever + "-" + $version_suffix + "." + $version.Revision.ToString().PadLeft(6,"0")
$mssemver = $basever + "-" + $version_suffix + "-" + $version.Revision.ToString().PadLeft(6,"0")
$appveyor_version = $mssemver

$Env:semver = $semver
$Env:mssemver = $mssemver
$Env:appveyor_version = $appveyor_version
$Env:ms_file_version = $version.ToString()

"Envrionment variable 'semver' set:" + $Env:semver
"Envrionment variable 'mssemver' set:" + $Env:mssemver
"Envrionment variable 'appveyor_version' set:" + $Env:appveyor_version