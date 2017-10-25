param ([string]$rsync, [string]$targetDirectory)

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

# path to rsync for windows (cygwin)
if ([String]::IsNullOrEmpty($rsync)) {
	$rsync = "rsync.exe";
}
# target directory for ConTeXt
if ([String]::IsNullOrEmpty($targetDirectory)) {
	$targetDirectory = $pwd;
}

# Source (server)
[string] $sourceServer = "contextgarden.net";
# Root on the context server
[string] $sourceRoot = "minimals/current/";

# Log-File
[string] $log = Join-Path $pwd "texmf-update.txt";

# remove old log
Remove-Item $log -ErrorAction SilentlyContinue;

function buildCygWinDrive([string] $path) {

	if (-not [System.IO.Path]::IsPathRooted($path)) {
		$path =[System.IO.Path]::GetFullPath($path);
	}

	[System.IO.FileInfo] $fi = New-Object -TypeName System.IO.FileInfo $path;
	if ($fi.Directory.Root.FullName.Length -ne 3) {
		return $null;
	}

	return "/cygdrive/" + $fi.FullName.Substring(0,1).ToLower() + $fi.FullName.Substring(2).Replace('\', '/') + "/.";
} # buildCygWinDrive

function rsyncInvoke([string[]] $sources, [string] $target, [string[]] $filter) {
	# create source
	$src = $sourceServer + "::'" + [String]::Join(' ', ($sources | foreach { $sourceRoot + $_})) + "'";
	
	# create destination
	$dst = Join-Path $targetDirectory $target;
	if (-not (Test-Path $dst)) {
		New-Item -ItemType Directory -Path $dst | Out-Null;
	}
	$dst = '"' + (buildCygWinDrive -path $dst) + '"';

	# build filter
	if ($filter.Length -gt 0) {
		$filterExpr = [String]::Join(" ", ($filter | foreach { "-f '" + $_ +"'" }));
	}

	# --list-only
	$expr = "&'$rsync' --recursive --compress --times --links --copy-links --verbose --delete --delete-excluded $filterExpr $src $dst";
	Invoke-Expression $expr | Tee-Object -FilePath $log -Append;
} # rsyncInvoke

# ###############################################################
# download texfm folder (fonts and metapost)
rsyncInvoke -sources 'base/tex/', 'base/metapost/', 'fonts/common/', 'fonts/other/', 'fonts/old/', 'fonts/new/', 'misc/web2c' -target "texmf"
rsyncInvoke -sources 'context/beta/' -target "texmf-context" -filter '- scripts/context/perl', '- scripts/context/ruby', '- scripts/context/stubs', '- tex/context/sample', '- tex/context/test', '- tex/context/bib', '- doc', '- bibtex', '- tex/latex';

$binFilter = @(
	'- bin/bibtex.exe', 
	'- bin/api-ms-win-crt-convert-l1-1-0.dll',
	'- bin/api-ms-win-crt-environment-l1-1-0.dll',
	'- bin/api-ms-win-crt-filesystem-l1-1-0.dll',
	'- bin/api-ms-win-crt-heap-l1-1-0.dll',
	'- bin/api-ms-win-crt-locale-l1-1-0.dll',
	'- bin/api-ms-win-crt-math-l1-1-0.dll',
	'- bin/api-ms-win-crt-multibyte-l1-1-0.dll',
	'- bin/api-ms-win-crt-process-l1-1-0.dll',
	'- bin/api-ms-win-crt-runtime-l1-1-0.dll',
	'- bin/api-ms-win-crt-stdio-l1-1-0.dll',
	'- bin/api-ms-win-crt-string-l1-1-0.dll',
	'- bin/api-ms-win-crt-time-l1-1-0.dll',
	'- bin/api-ms-win-crt-utility-l1-1-0.dll',
	'- bin/pstopdf.exe',
	'- bin/texexec.exe',
	'- bin/texluajit.exe',
	'- bin/texluajitc.exe',
	'- bin/contextjit.exe', 
	'- bin/luajittex.dll', 
	'- bin/luajittex.exe', 
	'- bin/lua'
);
rsyncInvoke -sources 'bin/common/mswin/', 'bin/context/mswin/', 'bin/metapost/mswin/', 'bin/luatex/mswin/' -target "texmf-win32" -filter $binFilter;
rsyncInvoke -sources 'bin/common/win64/', 'bin/context/win64/', 'bin/metapost/win64/', 'bin/luatex/win64/' -target "texmf-win64" -filter $binFilter;

# ###############################################################
# fix permissions
icacls.exe "$targetDirectory" /q /c /t /reset  | Tee-Object -FilePath $log -Append;

# ###############################################################
# clear cache
$cache = Join-Path $targetDirectory "texmf-cache";
if (Test-Path $cache) {
	Remove-Item -Path $cache -Recurse -Force;
}
# ###############################################################
# download modules

# ###############################################################
# create cache
$mtxrun = (Join-Path $targetDirectory "texmf-win64\bin\mtxrun.exe");
$genCommand1 = [String]::Join('', "& $mtxrun", " --tree='",$targetDirectory,"' --generate");
$genCommand2 = [String]::Join('', "& $mtxrun", " --tree='",$targetDirectory,"' --script context --autogenerate --make --engine=luatex cont-en");

#rem C:\Tools\MyContext\data2\tex\texmf-win64\bin\mtxrun.exe --tree="c:\Tools\MyContext\data\tex"  --direct --resolve mktexlsr
Invoke-Expression $genCommand1 | Tee-Object -FilePath $log -Append;
Invoke-Expression $genCommand2 | Tee-Object -FilePath $log -Append;
Invoke-Expression $genCommand1 | Tee-Object -FilePath $log -Append;

#rem rsync.exe -n rsync://contextgarden.net/minimals/current/modules/
#md tex\texmf-modules
#bin\rsync.exe -rztlv -L --delete contextgarden.net::'minimals/current/modules/f-urwgaramond/ minimals/current/modules/f-urwgothic/ minimals/current/modules/t-account/ minimals/current/modules/t-algorithmic/ minimals/current/modules/t-animation/ minimals/current/modules/t-annotation/ minimals/current/modules/t-bnf/ minimals/current/modules/t-chromato/ minimals/current/modules/t-cmscbf/ minimals/current/modules/t-cmttbf/ minimals/current/modules/t-construction-plan/ minimals/current/modules/t-cyrillicnumbers/ minimals/current/modules/t-degrade/ minimals/current/modules/t-fancybreak/ minimals/current/modules/t-filter/ minimals/current/modules/t-fixme/ minimals/current/modules/t-french/ minimals/current/modules/t-fullpage/ minimals/current/modules/t-games/ minimals/current/modules/t-gantt/ minimals/current/modules/t-layout/ minimals/current/modules/t-letter/ minimals/current/modules/t-lettrine/ minimals/current/modules/t-lilypond/ minimals/current/modules/t-mathsets/ minimals/current/modules/t-rst/ minimals/current/modules/t-simpleslides/ minimals/current/modules/t-title/ minimals/current/modules/t-transliterator/ minimals/current/modules/t-typearea/ minimals/current/modules/t-typescripts/ minimals/current/modules/t-visualcounter/' '/cygdrive/c/Tools/MyContext/data2/tex/texmf-modules'

