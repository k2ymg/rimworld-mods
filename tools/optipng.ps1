[cmdletBinding()]
param(
[parameter(mandatory=$true, position=0)]
[validateScript({
	test-path -literalPath $(resolve-path -literalPath $_) -pathType container
})]
[string]$texture_dir
)

$optipng = 'd:\tools\optipng\optipng.exe'
test-path -literalPath $optipng

get-childItem $texture_dir\*.png | foreach-object {
	$path = $_.fullName;
	$ret = start-process $optipng -argumentlist "-o7", """$path""" -passthru -wait -nonewwindow
}
