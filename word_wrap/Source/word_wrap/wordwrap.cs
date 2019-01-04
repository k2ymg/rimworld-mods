using System;
using System.Text;


namespace word_wrap
{
	static class WordWrap {
		private enum CharType {
			Word,
			OpenBracket,
			Other,
			JaKanji,
			JaHiraKata,
			ForceInclude,
		}

		private static CharType charType(int c)
		{
			if(c < 0x80){
				if(c < 0x20)
					return CharType.Other;

				switch(c){
				case 0x20: return CharType.Other;// space
				case 0x28: return CharType.OpenBracket;// (
				case 0x29: return CharType.ForceInclude;// )
				case 0x2d: return CharType.Other;// hyphen
				case 0x7f: return CharType.Other;
				}

				return CharType.Word;
			}

			if(c == 0x200b)// ZERO WIDTH SPACE
				return CharType.Other;

			if(c >= 0x3000 && c <= 0x303f){
				// CJK Symbols and Punctuation
				switch(c){
				//case 0x3000: return CharType.Space;//IDEOGRAPHIC SPACE
				case 0x3001: return CharType.ForceInclude;// IDEOGRAPHIC COMMA
				case 0x3002: return CharType.ForceInclude;// IDEOGRAPHIC FULL STOP
				case 0x3008: return CharType.OpenBracket;// LEFT ANGLE BRACKET
				case 0x3009: return CharType.ForceInclude;// RIGHT ANGLE BRACKET
				case 0x300c: return CharType.OpenBracket;// LEFT CORNER BRACKET
				case 0x300d: return CharType.ForceInclude;// RIGHT CORNER BRACKET
				}
				return CharType.Other;
			}

			if(c >= 0x3040 && c <= 0x30ff){
				return CharType.JaHiraKata;
			}

			if(c >= 0x4E00 && c <= 0x9FFF){
				// CJK Unified Ideographs
				return CharType.JaKanji;
			}

			return CharType.Word;
		}

		private static bool isSpace(int c)
		{
			if(c < 0x20){
				switch(c){
				case 0x0a:// LF
				case 0x0d:// CR
					return false;
				}
				return true;
			}

			switch(c){
			case 0x20:// space
			case 0x7f:// DEL
			case 0x200b:// ZERO WIDTH SPACE
			case 0x3000:// IDEOGRAPHIC SPACE (a.k.a. Zenkaku space)
				return true;
			}

			return false;
		}

		private static bool isDivider(int c)
		{
			if(c < 0x80){
				if(c < 0x20)
					return true;

				switch(c){
				case 0x20: return true;// space
				case 0x28: return true;// (
				//case 0x29:// )
				case 0x2d: return true;// hyphen
				case 0x7f: return true;
				}

				return false;
			}

			if(c == 0x200b)// ZERO WIDTH SPACE
				return true;

			if(c >= 0x3000 && c <= 0x30ff){
				// CJK Symbols and Punctuation
				// hira & kana
				return true;
			}
			if(c >= 0x4E00 && c <= 0x9FFF){
				// CJK Unified Ideographs
				return true;
			}

			return false;
		}

		private static bool isOopenBracket(int c)
		{
			switch(c){
			case 0x28:
			case 0x3008:
			//case 0x300a:
			case 0x300c:
			//case 0x300e:
			//case 0x3010:
				return true;
			}

			return false;
		}

		private const int JA_HIRAGANA_BIT    =  1;
		private const int JA_KATAKANA_BIT    =  2;
		private const int JA_HK_BIG_BIT      =  4;
		private const int JA_HK_SMALL_BIT    =  8;
		private const int JA_HK_SMALL_TU_BIT = 16;
		private static readonly byte[] sHiraKanaBigOrSmall = {
			0,
			JA_HK_SMALL_BIT,// a
			JA_HK_BIG_BIT,// A
			JA_HK_SMALL_BIT,// i
			JA_HK_BIG_BIT,// I
			JA_HK_SMALL_BIT,// u
			JA_HK_BIG_BIT,// U
			JA_HK_SMALL_BIT,// e
			JA_HK_BIG_BIT,// E
			JA_HK_SMALL_BIT,// o
			JA_HK_BIG_BIT,// O
			JA_HK_BIG_BIT,// KA
			JA_HK_BIG_BIT,// GA
			JA_HK_BIG_BIT,// KI
			JA_HK_BIG_BIT,// GI
			JA_HK_BIG_BIT,// KU

			JA_HK_BIG_BIT,// GU
			JA_HK_BIG_BIT,// KE
			JA_HK_BIG_BIT,// GE
			JA_HK_BIG_BIT,// KO
			JA_HK_BIG_BIT,// GO
			JA_HK_BIG_BIT,// SA
			JA_HK_BIG_BIT,// ZA
			JA_HK_BIG_BIT,// SI
			JA_HK_BIG_BIT,// ZI
			JA_HK_BIG_BIT,// SU
			JA_HK_BIG_BIT,// ZU
			JA_HK_BIG_BIT,// SE
			JA_HK_BIG_BIT,// ZE
			JA_HK_BIG_BIT,// SO
			JA_HK_BIG_BIT,// ZO
			JA_HK_BIG_BIT,// TA

			JA_HK_BIG_BIT,// DA
			JA_HK_BIG_BIT,// TI
			JA_HK_BIG_BIT,// DI
			JA_HK_SMALL_BIT | JA_HK_SMALL_TU_BIT,// tu
			JA_HK_BIG_BIT,// TU
			JA_HK_BIG_BIT,// DU
			JA_HK_BIG_BIT,// TE
			JA_HK_BIG_BIT,// DE
			JA_HK_BIG_BIT,// TO
			JA_HK_BIG_BIT,// DO
			JA_HK_BIG_BIT,// NA
			JA_HK_BIG_BIT,// NI
			JA_HK_BIG_BIT,// NU
			JA_HK_BIG_BIT,// NE
			JA_HK_BIG_BIT,// NO
			JA_HK_BIG_BIT,// HA

			JA_HK_BIG_BIT,// BA
			JA_HK_BIG_BIT,// PA
			JA_HK_BIG_BIT,// HI
			JA_HK_BIG_BIT,// BI
			JA_HK_BIG_BIT,// PI
			JA_HK_BIG_BIT,// HU
			JA_HK_BIG_BIT,// BU
			JA_HK_BIG_BIT,// PU
			JA_HK_BIG_BIT,// HE
			JA_HK_BIG_BIT,// BE
			JA_HK_BIG_BIT,// PE
			JA_HK_BIG_BIT,// HO
			JA_HK_BIG_BIT,// BO
			JA_HK_BIG_BIT,// PO
			JA_HK_BIG_BIT,// MA
			JA_HK_BIG_BIT,// MI

			JA_HK_BIG_BIT,// MU
			JA_HK_BIG_BIT,// ME
			JA_HK_BIG_BIT,// MO
			JA_HK_SMALL_BIT,// ya
			JA_HK_BIG_BIT,// YA
			JA_HK_SMALL_BIT,// yu
			JA_HK_BIG_BIT,// yu
			JA_HK_SMALL_BIT,// yo
			JA_HK_BIG_BIT,// YO
			JA_HK_BIG_BIT,// RA
			JA_HK_BIG_BIT,// RI
			JA_HK_BIG_BIT,// RU
			JA_HK_BIG_BIT,// RE
			JA_HK_BIG_BIT,// RO
			0,// wa
			JA_HK_BIG_BIT,// WA

			0,// WI
			0,// WE
			JA_HK_BIG_BIT,// WO
			JA_HK_BIG_BIT,// N
			JA_HK_BIG_BIT,// VU
			0,// ka
			0,// ke
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
		};

		private static int ja_hira_kata(int c)
		{
			if(c >= 0x3040 && c <= 0x30ff){
				if(c <= 0x309f){
					int f = sHiraKanaBigOrSmall[c - 0x3040];
					if(f != 0)
						return f | JA_HIRAGANA_BIT;
				}else{
					int f = sHiraKanaBigOrSmall[c - 0x30a0];
					if(f != 0)
						return f | JA_KATAKANA_BIT;
				}
			}
			return 0;
		}

		private static int ja_hiragana(int c)
		{
			if(c >= 0x3040 && c <= 0x309f){
				int f = sHiraKanaBigOrSmall[c - 0x3040];
				if(f != 0)
					return f | JA_HIRAGANA_BIT;
			}

			return 0;
		}

		/*private static int ja_katakana(int c)
		{
			if(c >= 0x30a0 && c <= 0x30ff){
				int f = sHiraKanaBigOrSmall[c - 0x30a0];
				if(f != 0)
					return f | JA_KATAKANA_BIT;
			}

			return 0;
		}*/

		private static bool ja_kanji(int c)
		{
			if(c >= 0x4E00 && c <= 0x9FFF){
				// CJK Unified Ideographs
				return true;
			}

			return false;
		}
	
		private static bool skipNewline(string str, ref int index)
		{
			int i = index;
			int c = str[i];

			switch(c){
			case 0x0a:// LF
				return true;

			case 0x0d:// CR
				if(i + 1 < str.Length && str[i + 1] == 0x0a)
					index++;
				return true;
			}

			return false;
		}

		private struct Tokenizer {
			private int m_next_index;
			private int m_new_line_count;
			private int m_chop_end;
			
			public int new_line_count
			{
				get{
					return m_new_line_count;
				}
			}

			public int chop_end
			{
				get{
					return m_chop_end;
				}
			}

			public int next_index
			{
				get{
					return m_next_index;
				}
			}

			Kerning kerning;

			public void init()
			{
				m_next_index = 0;
				m_new_line_count = 0;
				kerning = WordWrap_Unity.getKerning();
			}

			public bool nextChop(string str, int width_max)
			{
				int len = str.Length;
				int c;

				int start = next_index;
				int i = start;
				int width = 0;
				int prv_c = 0;
				int w;

			retry:
				for(; i < len; i++){
					c = WordWrap_Unity.takeCharAndWidth(str, ref i, out w);

					if(c == '\n'){
						goto new_line;
					}
					
					if(c == '\t'){
						int x = (width / w + 1) * w;
						w = x - width;
					}else{
						w -= kerning.offset(prv_c, c);
					}
					if(width + w > width_max){
						goto find_chop_point;
					}
					width += w;
				}
				return false;

			new_line:
				m_new_line_count++;
				start = i + 1;
				width = 0;
				i++;
				goto retry;

			find_chop_point:
				CharType cur_type;

				if(i == start){// too narrow
					i++;
					goto do_chop;
				}

				cur_type = charType(c);
				switch(cur_type){
				case CharType.OpenBracket: goto do_chop;
				case CharType.Other: goto open_bracket;
				case CharType.JaKanji: goto ja_kanji;
				case CharType.JaHiraKata: goto ja_hira_kata;
				case CharType.ForceInclude: goto force_include;
				}

				{
					int tmp_i = i;
					for(; i > start; i--){
						c = str[i - 1];
						if(isDivider(c))
							goto open_bracket;
					}
					i = tmp_i;
				}
				// too long
				goto do_chop;

			force_include:
				if(width + w / 3 <= width_max)
					i++;
				goto do_chop;

			ja_hira_kata:
				if(i - 4 > start){
					if(c == 0x30fc)// PROLONGED SOUND MARK
						goto ja_prolonged_sound_0;
					int f = ja_hira_kata(str[i]);
					if((f & JA_HK_SMALL_TU_BIT) != 0)
						goto ja_hk_small_tu;
					if((f & JA_HK_SMALL_BIT) != 0)
						goto ja_hk_small_0;

					// big.
					if((f & JA_KATAKANA_BIT) != 0){
						f = ja_hira_kata(str[i - 1]);
						if(f == (JA_KATAKANA_BIT | JA_HK_BIG_BIT)){
							f = ja_hira_kata(str[i - 2]);
							if((f & JA_KATAKANA_BIT) == 0)
								i = i - 1;
						}
					}else{
						// hiragana
						bool kanji = ja_kanji(str[i - 1]);
						if(kanji){
							kanji = ja_kanji(str[i - 2]);
							if(!kanji)
								i = i - 1;
						}else{
							f = ja_hiragana(str[i - 1]);
							if((f & JA_HK_BIG_BIT) != 0){
								kanji = ja_kanji(str[i - 2]);
								if(!kanji){
									f  = ja_hiragana(str[i - 2]);
									if(f == 0)
										i = i - 1;
								}
							}
						}
					}

					goto open_bracket;
				}
				goto do_chop;

			ja_hk_small_tu:
				{
					int c1 = str[i - 1];
					if(c1 == 0x30fc)// PROLONGED SOUND MARK
						goto ja_prolonged_sound_1;
					int f = ja_hira_kata(c1);
					if(f == 0){
						if(!ja_kanji(c1))
							goto open_bracket;
						int c2 = str[i - 2];
						if(!ja_kanji(c2)){
							i = i - 1;
							goto open_bracket;
						}
						int c3 = str[i - 3];
						if(!ja_kanji(c3))
							i = i - 2;
						goto open_bracket;
					}
					if((f & JA_HK_SMALL_TU_BIT) != 0)
						goto open_bracket;
					if((f & JA_HK_SMALL_BIT) != 0)
						goto ja_hk_small_1;
					i = i - 1;// BIG
				}
				goto open_bracket;

			ja_prolonged_sound_0:
				{
					int f = ja_hira_kata(str[i - 1]);
					if(f == 0)
						goto open_bracket;
					if((f & JA_HK_SMALL_TU_BIT) != 0)
						goto open_bracket;
					if((f & JA_HK_SMALL_BIT) != 0)
						goto ja_hk_small_1;
					i = i - 1;// BIG
				}
				goto open_bracket;

			ja_prolonged_sound_1:
				{
					int f = ja_hira_kata(str[i - 2]);
					if(f == 0)
						goto open_bracket;
					if((f & JA_HK_SMALL_TU_BIT) != 0)
						goto open_bracket;
					if((f & JA_HK_SMALL_BIT) != 0)
						goto ja_hk_small_2;
					i = i - 2;// BIG
				}
				goto open_bracket;

			ja_hk_small_0:
				{
					int f = ja_hira_kata(str[i - 1]);
					if((f & JA_HK_BIG_BIT) != 0)
						i = i - 1;
				}
				goto open_bracket;

			ja_hk_small_1:
				{
					int f = ja_hira_kata(str[i - 2]);
					if((f & JA_HK_BIG_BIT) != 0)
						i = i - 2;
				}
				goto open_bracket;

			ja_hk_small_2:
				{
					int f = ja_hira_kata(str[i - 3]);
					if((f & JA_HK_BIG_BIT) != 0)
						i = i - 3;
				}
				goto open_bracket;

			ja_kanji:
				if(i - 4 > start){
					bool kanji = ja_kanji(str[i - 1]);
					if(!kanji)
						goto open_bracket;
					kanji = ja_kanji(str[i - 2]);
					if(kanji)
						goto do_chop;

					i = i - 1;
					goto open_bracket;
				}
				goto do_chop;

			open_bracket:
				if(i - 4 > start){
					bool bracket = isOopenBracket(str[i - 1]);
					if(!bracket)
						goto do_chop;
					i = i - 1;
					goto do_chop;
				}
				goto do_chop;

			do_chop:
				if(i < len){
					if(skipNewline(str, ref i))
						goto new_line;
				}

				m_chop_end = i;
				if(i < len && isSpace(str[i])){
					i++;
					for(; i < len; i++){
						c = str[i];

						if(!isSpace(c)){
							if(skipNewline(str, ref i))
								i++;
							break;
						}
					}
				}
				m_next_index = i;
				
				return true;
			}
		}

		private static Tokenizer s_token;
		private static StringBuilder s_buf = new StringBuilder(256);// about

		public static string modify(string text, int width_max, out int line_count)
		{
			s_token.init();
			line_count = 1;

			try{
				if(!s_token.nextChop(text, width_max)){
					line_count += s_token.new_line_count;
					return null;
				}

				s_buf.Length = 0;
				int head = 0;

				for(;;){
					int copy_count = s_token.chop_end - head;
					if(copy_count > 0)
						s_buf.Append(text, head, copy_count);
					head = s_token.next_index;

					if(!s_token.nextChop(text, width_max))
						break;
					s_buf.Append('\n');
					line_count++;
				}

				if(head < text.Length){
					int copy_count = text.Length - head;
					s_buf.Append('\n');
					s_buf.Append(text, head, copy_count);
					line_count++;
				}

				line_count += s_token.new_line_count;
				return s_buf.ToString();

			}catch(ArgumentOutOfRangeException){
				return null;// error. I don't care of this.
			}
		}
	}
}