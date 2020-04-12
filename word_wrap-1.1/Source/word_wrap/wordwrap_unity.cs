using System;
using UnityEngine;


namespace word_wrap
{
	public class Kerning {
		int m_Ta;
		int m_Tc;
		int m_Te;
		int m_To;
		int m_Ts;
		int m_Ye;
		int m_Yo;
		int m_Yq;

		private static int str_width(string str, GUIStyle style, GUIContent c)
		{
			c.text = str;
			Vector2 sz = style.CalcSize(c);
			return (int)sz.x;
		}

		private static int char_width(char ch, Font font, int font_size, FontStyle font_style)
		{
			CharacterInfo ci;
			font.GetCharacterInfo(ch, out ci, font_size, font_style);
			return ci.advance;
		}

		public void setup(GUIStyle style)
		{
			GUIContent ct = new GUIContent();
			int old_top = style.padding.top;
			int old_bottom = style.padding.bottom;
			style.padding.top = 0;
			style.padding.bottom = 0;
			int font_size = style.fontSize;
			FontStyle font_style = style.fontStyle;
			Font font = style.font;

			int Ta = str_width("Ta", style, ct);
			int Tc = str_width("Tc", style, ct);
			int Te = str_width("Te", style, ct);
			int To = str_width("To", style, ct);
			int Ts = str_width("Ts", style, ct);
			int Ye = str_width("Ye", style, ct);
			int Yo = str_width("Yo", style, ct);
			int Yq = str_width("Yq", style, ct);

			int T = char_width('T', font, font_size, font_style);
			int Y = char_width('Y', font, font_size, font_style);
			int a = char_width('a', font, font_size, font_style);
			int c = char_width('c', font, font_size, font_style);
			int e = char_width('e', font, font_size, font_style);
			int o = char_width('o', font, font_size, font_style);
			int s = char_width('s', font, font_size, font_style);
			int q = char_width('q', font, font_size, font_style);

			m_Ta = T + a - Ta;
			m_Tc = T + c - Tc;
			m_Te = T + e - Te;
			m_To = T + o - To;
			m_Ts = T + s - Ts;
			m_Ye = Y + e - Ye;
			m_Yo = Y + o - Yo;
			m_Yq = Y + q - Yq;

			style.padding.top = old_top;
			style.padding.bottom = old_bottom;
		}

		public int offset(int c0, int c1)
		{
			switch(c0){
			case 'T':
				switch(c1){
				case 'a': return m_Ta;
				case 'c': return m_Tc;
				case 'e': return m_Te;
				case 'o': return m_To;
				case 's': return m_Ts;
				}
				break;

			case 'Y':
				switch(c1){
				case 'e': return m_Ye;
				case 'o': return m_Yo;
				case 'q': return m_Yq;
				}
				break;
			}

			return 0;
		}
	}

	static class WordWrap_Unity {
		static readonly byte[][] sAsciiWidth = new byte[3][];
		static readonly byte[][] sPuncHiraKataWidth = new byte[3][];
		static readonly Kerning[] sKerning = new Kerning[3];

		static Font s_font;
		static int s_font_size;
		static FontStyle s_font_style;
		static Kerning s_kerning;
		static byte[] s_ascii_width;
		static byte[] s_punc_hira_kata_width;

		static WordWrap_Unity()
		{
			for(int i = 0; i < 3; i++)
				sKerning[i] = new Kerning();
			for(int i = 0; i < 3; i++)
				sAsciiWidth[i] = new byte[96];
			for(int i = 0; i < 3; i++)
				sPuncHiraKataWidth[i] = new byte[256];
		}

		public static void setupFont(GUIStyle style, int font_index)
		{
			s_font = style.font;
			if(s_font == null)
				s_font = GUI.skin.font;
			s_font_size = style.fontSize;
			s_font_style = style.fontStyle;
			s_kerning = sKerning[font_index];
			s_ascii_width = sAsciiWidth[font_index];
			s_punc_hira_kata_width = sPuncHiraKataWidth[font_index];
		}

		public static int takeChar(string str, ref int index)
		{
			int i = index;
			char c = str[i];

			if(c < 0x80){
				if(c == '\r'){
					if(i + 1 < str.Length && str[i + 1] == '\n')
						index = i + 1;
					return '\n';
				}
			}else{
				// Unity does not support Surrogate
				if(char.IsSurrogate(c)){
					if(i + 1 < str.Length)
						index = i + 1;
					return '?';
				}
			}

			return c;
		}

		private static int cachedAscii(string str, int c)
		{
			if(c < 0x20 || c == 0x7f)
				return 0;

			int w = s_ascii_width[c - 0x20];
			if(w == 0){
				bool have = s_font.GetCharacterInfo((char)c, out s_ci, s_font_size, s_font_style);
				if(!have){
					s_font.RequestCharactersInTexture(str, s_font_size, s_font_style);
					have = s_font.GetCharacterInfo((char)c, out s_ci, s_font_size, s_font_style);
					if(!have)
						return 0;
				}
				w = s_ci.advance;
				s_ascii_width[c - 0x20] = (byte)w;
			}
			return w;
		}

		private static int cachedPuncHiraKata(string str, int c)
		{
			int w = s_punc_hira_kata_width[c - 0x3000];
			if(w == 0){
				bool have = s_font.GetCharacterInfo((char)c, out s_ci, s_font_size, s_font_style);
				if(!have){
					s_font.RequestCharactersInTexture(str, s_font_size, s_font_style);
					have = s_font.GetCharacterInfo((char)c, out s_ci, s_font_size, s_font_style);
					if(!have)
						return 0;
				}
				w = s_ci.advance;
				s_punc_hira_kata_width[c - 0x3000] = (byte)w;
			}
			return w;
		}

		static CharacterInfo s_ci;// for performance
		public static int takeCharAndWidth(string str, ref int index, out int width, int prv_c)
		{
			bool have;

			int c = takeChar(str, ref index);
			if(c < 0x80){
				if(c == '\t'){
					int w = cachedAscii(str, ' ');
					width = w * 16;
				}else{
					width = cachedAscii(str, c);
					width -= s_kerning.offset(prv_c, c);
				}
				return c;
			}

			if(c >= 0x3000 && c <= 0x30ff){
				width = cachedPuncHiraKata(str, c);
				return c;
			}

			have = s_font.GetCharacterInfo((char)c, out s_ci, s_font_size, s_font_style);
			if(!have){
				s_font.RequestCharactersInTexture(str, s_font_size, s_font_style);
				have = s_font.GetCharacterInfo((char)c, out s_ci, s_font_size, s_font_style);
				if(!have){
					width = 0;
					return c;
				}
			}
			width = s_ci.advance;

			return c;
		}

		public static void setupKerning(int index, GUIStyle style)
		{
			sKerning[index].setup(style);
		}
	}
}
