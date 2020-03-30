param ([string]$targetDirectory, [string]$version)

#
# Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
# European Commission - subsequent versions of the EUPL(the "Licence"); You may
# not use this work except in compliance with the Licence.
#
# You may obtain a copy of the Licence at:
# http://ec.europa.eu/idabc/eupl
#
# Unless required by applicable law or agreed to in writing, software distributed
# under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
# CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
# specific language governing permissions and limitations under the Licence.
#

#
# convert schema to xsd with
#
# Trang
# Copyright © 2002, 2003, 2008 Thai Open Source Software Center Ltd
#
# java -jar C:\Tools\trang\trang.jar share\schema\layoutschema-en.rng share\schema\layoutschema-en.xsd
#
# patch: bei foreign-elements <xs:any namespace="##other" minOccurs="0" maxOccurs="unbounded" processContents="skip"/>
#        del: <xs:import namespace="http://www.w3.org/XML/1998/namespace" schemaLocation="xml.xsd"/>
#        del: <xs:attribute ref="xml:base"/>
#

Import-Module BitsTransfer

Add-Type -AssemblyName "System.IO.Compression, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
Add-Type -AssemblyName "System.IO.Compression.FileSystem, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";

[string[]] $unpackFilter = @(
	'speedata-publisher/bin/',
	'speedata-publisher/sdluatex/',
	'speedata-publisher/sw/fonts/',
	'speedata-publisher/sw/hyphenation/'
	'speedata-publisher/sw/img/filenotfound.pdf',
	'speedata-publisher/sw/lua/',
	'speedata-publisher/sw/tex/',
	'speedata-publisher/share/schema/layoutschema-en.rng'
);
[string] $unpackFilterPathOffset = 'speedata-publisher\'.Length;


if ([String]::IsNullOrEmpty($version)) {
	$version = "3.8.0";
}

# target directory for speedata
if ([String]::IsNullOrEmpty($targetDirectory)) {
	$targetDirectory = $pwd;
}

# download source
[string] $name = "speedata-publisher-windows-386-$($version).zip";
[string] $baseUri = "https://download.speedata.de/dl/$($name)"
[string] $tempDestination = Join-Path ([System.IO.Path]::GetTempPath()) $name;

if (-not [System.IO.File]::Exists($tempDestination)) {
	Write-Host "Download source $($baseUri)...";
	Start-BitsTransfer -Source $baseUri -Destination $tempDestination;
}

# unpack zip file
function IsFiltered([string]$path) {

	foreach ($f in $unpackFilter) {
		if ($path.StartsWith($f, [System.StringComparison]::OrdinalIgnoreCase) -And -Not $path.EndsWith('.gitignore', [System.StringComparison]::OrdinalIgnoreCase)) {
			return $path.Substring($unpackFilterPathOffset);
		}
	}

	return $null;
} # IsFiltered

[System.IO.Compression.ZipArchive] $zip = [System.IO.Compression.ZipFile]::Open($tempDestination, [System.IO.Compression.ZipArchiveMode]::Read);
try {
	foreach ($cur in $zip.Entries) {

		# no directories
		if ($cur.FullName.EndsWith('/')) {
			continue;
		}

		# test filter
		[string] $target = IsFiltered($cur.FullName);
		if (-not [string]::IsNullOrEmpty($target)) {
			$fi = New-Object -TypeName System.IO.FileInfo -ArgumentList (Join-Path $targetDirectory $target);
			if (!($fi.Directory.Exists)) {
				$fi.Directory.Create();
			}
			
			if ($fi.LastWriteTime -lt $cur.LastWriteTime.DateTime -or $fi.Length -ne $cur.Length) {

				Write-Host "Unpack: $($cur.FullName)";

				if ($fi.Exists) {
					$fi.Delete();
				}

				$dst = $fi.OpenWrite();
				$src = $cur.Open();
				try {
					$src.CopyTo($dst);
				} finally {
					$dst.Dispose();
					$src.Dispose();
				}

				# update LastWriteTime
				$fi.Refresh();
				$fi.LastWriteTime = $cur.LastWriteTime.DateTime;
			} else {
				Write-Host "Exists: $($cur.FullName)";
			}
		} else {
			Write-Host "Ignore: $($cur.FullName)";
		}
	}
} finally {
	$zip.Dispose();
}