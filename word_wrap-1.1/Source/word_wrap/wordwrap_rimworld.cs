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
		
		static readonly int[] s_mysteriousHeightOffset = new int[3];
		static readonly char[] s_CRLF = {'\r', '\n'};
		static readonly Cache s_cache = new Cache();

		static WordWrap_RimWorld()
		{
			HarmonyInstance harmony = HarmonyInstance.Create("com.github.k2ymg.wordwrap");

			Type patch_class = typeof(WordWrap_RimWorld);

			Type type;
			MethodInfo original, target;

			type = typeof(Text);

			original = type.GetMethod("StartOfOnGUI");
			target = patch_class.GetMethod("Text_StartOfOnGUI_postfix");
			var ret = harmony.Patch(original, null, new HarmonyMethod(target));

			original = type.GetMethod("CalcHeight", new Type[] {typeof(string), typeof(float)});
			target = patch_class.GetMethod("Text_CalcHeight_prefix");
			harmony.Patch(original, new HarmonyMethod(target));
			
			original = type.GetMethod("CalcSize", new Type[] {typeof(string)});
			target = patch_class.GetMethod("Text_CalcSize_prefix");
			harmony.Patch(original, new HarmonyMethod(target));

			type = typeof(Widgets);

			original = type.GetMethod("Label", new Type[] {typeof(Rect), typeof(string)});
			target = patch_class.GetMethod("Widgets_Label_prefix");
			harmony.Patch(original, new HarmonyMethod(target));
		}

		private static void setupKerning()
		{
			for(int i = 0; i < 3; i++){
				GUIStyle style = Text.fontStyles[i];
				WordWrap_Unity.setupKerning(i, style);
			}
		}

		private static void setupMysteriousHeightOffset()
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
		}

		private static void init_once()
		{
			if(s_initialized)
				return;
			s_initialized = true;
			setupMysteriousHeightOffset();
			setupKerning();
		}

		private static int calcHeight(GUIStyle style, int font_index, int line_count)
		{
			return (int)style.lineHeight * line_count
				+ style.padding.vertical + s_mysteriousHeightOffset[font_index];
		}

		private static int lineCount(string str)
		{
			int line_count = 1;

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

			return line_count;
		}

		public static void Text_StartOfOnGUI_postfix()
		{
			init_once();

			if(Event.current.type == EventType.Repaint){
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

			{
				CacheData cache = s_cache.getData(text, font_index);

				if(style.wordWrap){
					if(cache.wrap_line_count == 0 || cache.wrap_width != (int)width){
						WordWrap_Unity.setupFont(style, font_index);
						cache.wrap_str = WordWrap.wrap(style.richText, text, (int)width, out cache.wrap_line_count);
						cache.wrap_width = (int)width;
					}
					line_count = cache.wrap_line_count;
				}else{
					if(cache.line_count == 0){
						cache.line_count = lineCount(text);
						cache.str = text;
					}
					line_count = cache.line_count;
				}
			}

			__result = calcHeight(style, font_index, line_count);

			return false;
		}

		public static bool Text_CalcSize_prefix(string text, ref Vector2 __result)
		{
			if(string.IsNullOrEmpty(text))
				return false;

			int font_index = (int)Text.Font;
			GUIStyle style = Text.CurFontStyle;

			CacheData cache = s_cache.getData(text, font_index);

			if(cache.line_count == 0 || cache.width == 0){
				WordWrap_Unity.setupFont(style, font_index);
				cache.width = WordWrap.calcSize(style.richText, text, out int line_count);
				cache.line_count = line_count;
				cache.str = text;
			}

			__result.x = cache.width + style.padding.horizontal;
			__result.y = calcHeight(style, font_index, cache.line_count);

			return false;
		}

		public static bool Widgets_Label_prefix(Rect rect, string label)
		{
			if(Event.current.type != EventType.Repaint)
				return false;

			if(string.IsNullOrEmpty(label))
				return false;

			float scale = Prefs.UIScale;
			if(scale > 1f){
				float num = scale / 2f;
				if(Math.Abs(num - Mathf.Floor(num)) > Single.Epsilon){
					rect.xMin = Widgets.AdjustCoordToUIScalingFloor(rect.xMin);
					rect.yMin = Widgets.AdjustCoordToUIScalingFloor(rect.yMin);
					rect.xMax = Widgets.AdjustCoordToUIScalingCeil(rect.xMax + 1E-05f);// + 0.00001f
					rect.yMax = Widgets.AdjustCoordToUIScalingCeil(rect.yMax + 1E-05f);
				}
			}

			int font_index = (int)Text.Font;
			GUIStyle style = Text.CurFontStyle;
			int line_count;

			{
				CacheData cache = s_cache.getData(label, font_index);
				if(style.wordWrap){
					if(cache.wrap_line_count == 0 || cache.wrap_width != (int)rect.width){
						WordWrap_Unity.setupFont(style, font_index);
						cache.wrap_str = WordWrap.wrap(style.richText, label, (int)rect.width, out cache.wrap_line_count);
						cache.wrap_width = (int)rect.width;
					}
					label = cache.wrap_str;
					line_count = cache.wrap_line_count;
				}else{
					if(cache.line_count == 0){
						cache.line_count = lineCount(label);
						cache.str = label;
					}
					label = cache.str;
					line_count = cache.line_count;
				}
			}

			
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
			GUI.Label(rect, label, style);
			style.wordWrap = ww;
			style.alignment = alignment;

			return false;
		}
    }
}
