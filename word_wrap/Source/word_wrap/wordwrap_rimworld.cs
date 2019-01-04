using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Harmony;
using UnityEngine;
using Verse;


namespace word_wrap
{
	[StaticConstructorOnStartup]
    public static class WordWrap_RimWorld
    {
		static bool s_initialized;
		static bool s_os_font_loaded;
		static HarmonyInstance s_harmony;
		
		static readonly int[] s_mysteriousHeightOffset = new int[3];
		static readonly Cache s_cache = new Cache();
		static readonly char[] s_CRLF = {'\r', '\n'};
		static readonly GUIContent s_content = new GUIContent();

		static WordWrap_RimWorld()
		{
			s_harmony = HarmonyInstance.Create("com.github.k2ymg.wordwrap");

			Type patch_class = typeof(WordWrap_RimWorld);

			Type type;
			MethodInfo original, target;
			
			type = typeof(Text);

			original = type.GetMethod("StartOfOnGUI");
			target = patch_class.GetMethod("Text_StartOfOnGUI_postfix");
			var ret = s_harmony.Patch(original, null, new HarmonyMethod(target));
		}

		static void setupOSFont()
		{
			ModContentPack this_mod;
			List<ModContentPack> mods = LoadedModManager.RunningModsListForReading;
			foreach(ModContentPack mod in mods){
				if(mod.Name == "Word Wrap"){
					this_mod = mod;
					goto find;
				}
			}
			Log.Warning("Not found MOD myself");
			return;

		find:
			string path = Path.Combine(this_mod.RootDir, "Other/font.txt");
			string font_name = null;
			int[] font_size = new int[3];
			int[] font_pad = new int[3];

			for(int i = 0; i < 3; i++){
				font_size[i] = Text.fontStyles[i].font.fontSize;
				//Log.Warning("Original Font size = " + font_size[i]);
				font_pad[i] = Text.fontStyles[i].padding.top;
			}

			try{
				using(StreamReader file = new StreamReader(path)){
					for(;;){
						string l = file.ReadLine();
						if(l == null)
							break;
						string[] v = l.Split(':');
						if(v.Length < 2)
							return;
						v[0] = v[0].Trim();
						if(v[0][0] == ';')
							continue;
						switch(v[0]){
						case "font_name":
							font_name = v[1];
							break;
						case "tiny_size":
							font_size[0] = Int32.Parse(v[1]);
							break;
						case "tiny_pad":
							font_pad[0] = Int32.Parse(v[1]);
							break;
						case "small_size":
							font_size[1] = Int32.Parse(v[1]);
							break;
						case "small_pad":
							font_pad[1] = Int32.Parse(v[1]);
							break;
						case "medium_size":
							font_size[2] = Int32.Parse(v[1]);
							break;
						case "medium_pad":
							font_pad[2] = Int32.Parse(v[1]);
							break;
						default:
							Log.Warning("unknown name:" + v[0]);
							break;
						}
					}
				} 
			}catch{
				return;
			}
			if(font_name == null)
				return;

			int size = font_size[0];
			Font font = Font.CreateDynamicFontFromOSFont(font_name, size);
			// This is not occurred an error even the font doen't exist. faq.
			s_os_font_loaded = true;

			for(int i = 0; i < 3; i++){
				size = font_size[i];
				int pad = font_pad[i];
				Text.fontStyles[i].font = font;
				Text.fontStyles[i].fontSize = size;
				Text.fontStyles[i].padding.top = pad;
				Text.fontStyles[i].padding.bottom = pad;
				Text.textFieldStyles[i].font = font;
				Text.textFieldStyles[i].fontSize = size;
				Text.textFieldStyles[i].padding.top = pad;
				Text.textFieldStyles[i].padding.bottom = pad;
				Text.textAreaStyles[i].font = font;
				Text.textAreaStyles[i].fontSize = size;
				Text.textAreaStyles[i].padding.top = pad;
				Text.textAreaStyles[i].padding.bottom = pad;
				Text.textAreaReadOnlyStyles[i].font = font;
				Text.textAreaReadOnlyStyles[i].fontSize = size;
				Text.textAreaReadOnlyStyles[i].padding.top = pad;
				Text.textAreaReadOnlyStyles[i].padding.bottom = pad;
				//Resources.UnloadAsset(old_font);
			}
		}

		static void setupKerning()
		{
			for(int i = 0; i < 3; i++){
				GUIStyle style = Text.fontStyles[i];
				WordWrap_Unity.setupKerning(i, style);
			}
		}

		static void patch_2()
		{
			Type patch_class = typeof(WordWrap_RimWorld);

			Type type;
			MethodInfo original, target;
			
			type = typeof(Text);

			original = type.GetMethod("CalcHeight", new Type[] {typeof(string), typeof(float)});
			target = patch_class.GetMethod("Text_CalcHeight_prefix");
			s_harmony.Patch(original, new HarmonyMethod(target));
			
			original = type.GetMethod("CalcSize", new Type[] {typeof(string)});
			target = patch_class.GetMethod("Text_CalcSize_prefix");
			s_harmony.Patch(original, new HarmonyMethod(target));

			type = typeof(Widgets);

			original = type.GetMethod("Label", new Type[] {typeof(Rect), typeof(string)});
			target = patch_class.GetMethod("Widgets_Label_prefix");
			s_harmony.Patch(original, new HarmonyMethod(target));
		}

		static void setupMysteryHeightOffset()
		{
			GUIContent c = new GUIContent("A");
			RectOffset padding_zero = new RectOffset(0, 0, 0, 0);

			for(int i = 0; i < 3; i++){
				GUIStyle style = Text.fontStyles[i];
				int old_top = style.padding.top;
				int old_bottom = style.padding.bottom;

				style.padding = padding_zero;
				Vector2 size = style.CalcSize(c);
				s_mysteriousHeightOffset[i] = (int)size.y - (int)style.lineHeight;

				style.padding.top = old_top;
				style.padding.bottom = old_bottom;
			}

			if(s_os_font_loaded){
				Traverse t = Traverse.Create(typeof(Text));
				float[] lineHeights = t.Field("lineHeights").GetValue<float[]>();
				float[] spaceBetweenLines = t.Field("spaceBetweenLines").GetValue<float[]>();

				for(int i = 0; i < 3; i++){
					// in Original, spaceBetweenLines is negative value. I'm not sure it correct or not.
					GUIStyle style = Text.fontStyles[i];
					float h = calcHeight(style, i, 1);//Text.CalcHeight("W", 999f);
					float hh = calcHeight(style, i, 2);//Text.CalcHeight("W\nW", 999f);
					lineHeights[i] = h;
					spaceBetweenLines[i] = hh - h * 2f;
				}
			}
		}

		static int calcHeight(GUIStyle style, int font_index, int line_count)
		{
			return (int)style.lineHeight * line_count
				+ style.padding.vertical + s_mysteriousHeightOffset[font_index];
		}

		static void s_calcSize(GUIStyle style, int font_index, string str, CacheData cache)
		{
			int line_count = 1;

			s_content.text = str;
			Vector2 size = style.CalcSize(s_content);

			// bug fix
			int index = str.IndexOfAny(s_CRLF);
			while(index >= 0){
				line_count++;

				if(str[index] == '\r'){
					index++;
					if(index < str.Length && str[index] == '\n')
						index++;
				}else{
					index++;
				}
				if(index >= str.Length)
					break;

				index = str.IndexOfAny(s_CRLF, index);
			}

			cache.width = (int)size.x;
			cache.line_count = line_count;
		}

		public static void Text_StartOfOnGUI_postfix()
		{
			if(!s_initialized){
				s_initialized = true;
				
				setupOSFont();
				setupKerning();
				setupMysteryHeightOffset();

				patch_2();
			}

			if(Event.current.type == EventType.repaint){
				s_cache.clean();
			}
		}
		
		public static bool Text_CalcHeight_prefix(string text, float width, ref float __result)
		{
			if(string.IsNullOrEmpty(text))
				return false;

			int font_index = (int)Text.Font;
			GUIStyle style = Text.CurFontStyle;
			int line_count;
			string str = text;

			CacheData cache = s_cache.getData(str, font_index);
			
			if(style.wordWrap){
				if(cache.width != 0 && cache.width <= (int)width){
					line_count = cache.line_count;
					goto end;
				}

				if(
					(cache.wrap_width == (int)width) ||
					(cache.wrap_width != 0 && cache.wrap_str == null && cache.wrap_width <= (int)width)
					)
				{
					line_count = cache.wrap_line_count;
					goto end;
				}

				WordWrap_Unity.setupFont(style, font_index);
				string new_label = WordWrap.modify(str, (int)width, out line_count);
				cache.wrap_width = (int)width;
				cache.wrap_line_count = line_count;
				cache.wrap_str = new_label;

				line_count = cache.wrap_line_count;
			}else{
				if(cache.width == 0)
					s_calcSize(style, font_index, str, cache);

				line_count = cache.line_count;
			}
		end:
			__result = calcHeight(style, font_index, line_count);

			return false;
		}

		public static bool Text_CalcSize_prefix(string text, ref Vector2 __result)
		{
			if(string.IsNullOrEmpty(text))
				return false;

			int font_index = (int)Text.Font;
			GUIStyle style = Text.CurFontStyle;

			string str = text;

			CacheData cache = s_cache.getData(str, font_index);
			if(cache.width == 0){
				s_calcSize(style, font_index, str, cache);
			}

			__result.x = cache.width;
			__result.y = calcHeight(style, font_index, cache.line_count);

			return false;
		}


		public static bool Widgets_Label_prefix(Rect rect, string label)
		{
			if(Event.current.type != EventType.Repaint)
				return false;

			if(string.IsNullOrEmpty(label))
				return false;

			int font_index = (int)Text.Font;
			GUIStyle style = Text.CurFontStyle;

			string str = label;
			int width = (int)rect.width;
				
			int line_count;

			CacheData cache = s_cache.getData(str, font_index);
			if(style.wordWrap){
				if(cache.width != 0 && cache.width <= width){
					line_count = cache.line_count;
					goto end;
				}

				if(
					(cache.wrap_width == width) ||
					(cache.wrap_width != 0 && cache.wrap_str == null && cache.wrap_width <= width)
					)
				{
					line_count = cache.wrap_line_count;
					if(cache.wrap_str != null)
						str = cache.wrap_str;
					goto end;
				}

				WordWrap_Unity.setupFont(style, font_index);
				string new_label = WordWrap.modify(str, width, out line_count);

				cache.wrap_width = (int)width;
				cache.wrap_line_count = line_count;
				cache.wrap_str = new_label;

				line_count = cache.wrap_line_count;
				if(cache.wrap_str != null)
					str = cache.wrap_str;

			}else{
				if(cache.width == 0)
					s_calcSize(style, font_index, str, cache);
				line_count = cache.line_count;
			}
		end:
			int text_height = calcHeight(style, font_index, line_count);

			TextAnchor alignment = style.alignment;
			float offset_y;
			switch(alignment){
			case TextAnchor.MiddleLeft:
				offset_y = rect.height / 2 - text_height / 2;
				style.alignment = TextAnchor.UpperLeft;
				break;

			case TextAnchor.MiddleCenter:
				offset_y = rect.height / 2 - text_height / 2;
				style.alignment = TextAnchor.UpperCenter;
				break;

			case TextAnchor.MiddleRight:
				offset_y = rect.height / 2 - text_height / 2;
				style.alignment = TextAnchor.UpperRight;
				break;

			case TextAnchor.LowerLeft:
				offset_y = rect.height - text_height;
				style.alignment = TextAnchor.UpperLeft;
				break;

			case TextAnchor.LowerCenter:
				offset_y = rect.height - text_height;
				style.alignment = TextAnchor.UpperCenter;
				break;

			case TextAnchor.LowerRight:
				offset_y = rect.height - text_height;
				style.alignment = TextAnchor.UpperRight;
				break;

			default:
				offset_y = 0;
				break;
			}
			rect.y += offset_y;
				
			bool ww = style.wordWrap;
			style.wordWrap = false;
			GUI.Label(rect, str, style);
			style.wordWrap = ww;
			style.alignment = alignment;

			return false;
		}
    }
}
