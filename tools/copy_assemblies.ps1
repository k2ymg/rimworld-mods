
$src='C:\Program Files (x86)\Steam\SteamApps\common\RimWorld\RimWorldWin64_Data\Managed'
$dst='..\rimworld-assemblies'

mkdir -force $dst | out-null

@('Assembly-CSharp.dll', 'UnityEngine.dll') | foreach-object {
	
	copy-item $(join-path $src $_) $dst -force
}
