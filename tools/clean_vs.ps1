get-childItem -literalPath .. -directory | foreach-object {
	$path = $_.fullName;
	remove-Item $(join-path $path Assemblies\*.pdb) -errorAction silentlyContinue
	remove-Item $(join-path $path Source\*\.vs) -recurse -force -errorAction silentlyContinue
	remove-Item $(join-path $path Source\*\bin) -recurse -force -errorAction silentlyContinue
	remove-Item $(join-path $path Source\*\obj) -recurse -force -errorAction silentlyContinue
}
