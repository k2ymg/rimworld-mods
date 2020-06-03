
$src='C:\Program Files (x86)\Steam\SteamApps\common\RimWorld\RimWorldWin64_Data\Managed'
$dst='..\rimworld-assemblies-1.1'

mkdir -force $dst | out-null

@('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.IMGUIModule.dll', 'UnityEngine.TextRenderingModule.dll') | foreach-object {
	
	copy-item $(join-path $src $_) $dst -force
}
