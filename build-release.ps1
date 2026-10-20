# Builds the release packages into artifacts/<version>/ (version from Directory.Build.props).
# Runs on Windows PowerShell 5.1 and PowerShell 7 (also on Linux/macOS: the Windows
# packages can be built there thanks to EnableWindowsTargeting).
#
#   UOArchitect-<v>-win-x64.zip                  GUI, self-contained, no .NET install needed
#   UOArchitect-<v>-win-x86.zip                  GUI, self-contained, 32-bit Windows
#   UOArchitect-<v>-win-arm64.zip                GUI, self-contained, Windows on ARM
#   UOArchitect-<v>-win-x64-framework-dependent.zip  GUI, small, needs .NET 10 Desktop Runtime
#   UOArchitect-<v>-server-net10.zip             OrbServerSDK + UOArchitectInterface (any OS/CPU) + server script
#   SHA256SUMS.txt

param(
	[string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = $PSScriptRoot

[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
$out = Join-Path $root "artifacts/$version"
$gui = Join-Path $root "UO Architect/UO Architect.csproj"
$interface = Join-Path $root "UO Architect/UOArchitectInterfaces/UOArchitectInterface.csproj"

function Invoke-DotNet {
	& dotnet @args
	if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') failed ($LASTEXITCODE)" }
}

function New-Zip([string]$dir) {
	$zip = "$dir.zip"
	if (Test-Path $zip) { Remove-Item $zip -Force }
	# entries are written one by one with '/' separators; Compress-Archive and ZipFile in
	# PowerShell 5.1 write '\', which other unzip tools turn into file names
	$base = (Resolve-Path $dir).Path.TrimEnd('\', '/')
	$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
	try {
		foreach ($file in Get-ChildItem $base -Recurse -File) {
			$name = $file.FullName.Substring($base.Length + 1).Replace('\', '/')
			[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
		}
	}
	finally {
		$archive.Dispose()
	}
	return $zip
}

New-Item -ItemType Directory -Force $out | Out-Null
Get-ChildItem $out | Remove-Item -Recurse -Force
$zips = @()

# GUI, self-contained single file per Windows CPU
foreach ($rid in @("win-x64", "win-x86", "win-arm64")) {
	$dir = Join-Path $out "UOArchitect-$version-$rid"
	Invoke-DotNet publish $gui -c $Configuration -r $rid --self-contained `
		-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
		-p:EnableCompressionInSingleFile=true -p:DebugType=none -o $dir
	$zips += New-Zip $dir
}

# GUI, framework-dependent (needs the .NET 10 Desktop Runtime)
$dir = Join-Path $out "UOArchitect-$version-win-x64-framework-dependent"
Invoke-DotNet publish $gui -c $Configuration -r win-x64 --no-self-contained -p:DebugType=none -o $dir
$zips += New-Zip $dir

# Server side: plain net10.0 libraries, the same files for every OS and CPU
$dir = Join-Path $out "UOArchitect-$version-server-net10"
$build = Join-Path $out "server-build"
Invoke-DotNet build $interface -c $Configuration -p:DebugType=none -o $build
New-Item -ItemType Directory -Force $dir | Out-Null
Copy-Item (Join-Path $build "OrbServerSDK.dll"), (Join-Path $build "UOArchitectInterface.dll") $dir
Copy-Item (Join-Path $root "OrbScriptServerSDK/Orb UOAR Server Script/OrbServer.cs") $dir
Copy-Item (Join-Path $root "release/SERVER-INSTALL.txt") $dir
Remove-Item $build -Recurse -Force
$zips += New-Zip $dir

# Checksums
$sums = foreach ($zip in $zips) {
	$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
	"$hash  $(Split-Path $zip -Leaf)"
}
$sums | Set-Content -Encoding ascii (Join-Path $out "SHA256SUMS.txt")

Write-Host ""
Write-Host "Release $version -> $out"
Get-ChildItem $out -Filter *.zip | ForEach-Object { "{0,10:N0} kB  {1}" -f ($_.Length / 1kB), $_.Name }
