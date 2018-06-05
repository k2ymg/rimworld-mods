if(-not $(test-path About)){
	write-host 'no About'
	exit
}

$dir_name = $(split-path $(get-location) -leaf)
$dst_root='C:\Program Files (x86)\Steam\SteamApps\common\RimWorld\Mods'
$dst = join-path $dst_root $dir_name

# clean
if(test-path $dst){
 	remove-item $dst\* -recurse -exclude PublishedFileId.txt
}

# copy
copy-item About $dst -recurse -force
copy-item Assemblies $dst -recurse -force -errorAction silentlyContinue -exclude *.pdb
@('Defs', 'Languages', 'Patches', 'Textures') | foreach-object {
	copy-item $_ $dst -recurse -force -errorAction silentlyContinue
}
