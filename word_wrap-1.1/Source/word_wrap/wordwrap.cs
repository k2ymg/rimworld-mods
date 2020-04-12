using System;
using System.Text;


namespace word_wrap
{
	static class WordWrap {
		private const int CT_SPACE = 0;
		private const int CT_HARD  = 1;
		private const int CT_SOFT  = 2;

		private static int charType(int c)
		{
			switch(c){
			case 0x09:// horizontal tab
			case 0x20:// space
			case 0x200b:// ZERO WIDTH SPACE
			case 0x3000:// IDEOGRAPHIC SPACE (a.k.a. Zenkaku space)
				return CT_SPACE;
			}

			if(c >= 0x3000 && c <= 0x30ff){
				// CJK Symbols and Punctuation, Hira and Kata
				return CT_SOFT;
			}
			if(c >= 0x4E00 && c <= 0x9FFF){
				// CJK Unified Ideographs, a.k.a Kanji
				return CT_SOFT;
			}
			if(c >= 0xff00 && c <= 0xffef){
				// Halfwidth and Fullwidth Forms, a.k.a Zenkaku and Hankaku
				return CT_SOFT;
			}

			return CT_HARD;
		}

		private static bool isDoNotSplit(int c)
		{
			if(0x3040 <= c && c <= 0x30ff){// Hira and kana
				if(c == 0x30fc)// PROLONGED SOUND MARK
					return true;

				if(c >= 0x30a0)
					c -= 0x30a0;
				else
					c -= 0x3040;
				switch(c){
				case 0x01:// small a
				case 0x03:// small i
				case 0x05:// small u
				case 0x07:// small e
				case 0x09:// small o
				case 0x23:// small thu
				case 0x43:// small ya
				case 0x45:// small yu
				case 0x47:// small yo
					return true;
				}
				return false;
			}

			return false;
		}

		private static bool isOpen(int c)
		{
			switch(c){
			case 0x300c:// left corner bracket
			case 0xff08:// fullwidth left parenthesis
				return true;
			}

			return false;
		}

		private static bool isClose(int c)
		{
			switch(c){
			case 0x3001:// comma
			case 0x3002:// full stop
			case 0x300d:// right corner bracket
			case 0xff09:// fullwidth right parenthesis
				return true;
			}

			return false;
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

		private struct Tokenizer {
			private bool m_tag;
			private string m_str;
			private int m_len;
			private int m_max_width;
			public int m_chop0 {get; private set;}
			public int m_chop1 {get; private set;}
			private int m_next_chop0;
			private int m_next_index;
			private int m_next_width;
			private int m_next_prev_char_type;
			private int m_next_prev_c;
			public int m_line_count {get; private set;}

			public void init(bool tag, string str, int max_width)
			{
				m_tag = tag;
				m_str = str;
				m_len = str.Length;
				m_max_width = max_width;
				m_chop0 = 0;
				m_chop1 = -1;

				m_next_chop0 = 0;
				m_next_index = 0;
				m_next_width = 0;
				m_next_prev_char_type = 0;
				m_next_prev_c = 0;
				m_line_count = 1;
			}

			public bool nextChop()
			{
				m_chop0 = m_next_chop0;

				int i = m_next_index;
				int width = m_next_width;
				int prev_char_type = m_next_prev_char_type;
				int prev_c = m_next_prev_c;

				int chop_index = -1;
				int chop_width = -1;
				int w, c;
				int char_type;

				for(; i < m_len; i++){
					c = m_str[i];

					if(c == '<' && m_tag){
						i++;
						if(i >= m_len)
							break;
						i = m_str.IndexOf('>', i);
						if(i < 0){
							m_chop1 = m_len;
							return false;
						}
						prev_c = 0;
						continue;
					}

					c = WordWrap_Unity.takeCharAndWidth(m_str, ref i, out w, prev_c);
					prev_c = c;

					if(c == '\n'){
						chop_index = -1;
						width = 0;
						m_line_count++;
						continue;
					}

					if(c == '\t'){
						int x = (width / w + 1) * w;
						w = x - width;
					}

					width += w;

					char_type = charType(c);
					if(char_type != CT_HARD){
						chop_index = i;
						chop_width = width;
					}
					if(width > m_max_width){
						goto find_chop_point;
					}
				}

				m_chop1 = i;
				return false;

			find_chop_point:
				if(m_chop0 == i){
					m_chop1 = i + 1;
					m_next_index = i + 1;
					m_next_chop0 = i + 1;
					m_next_width = 0;
					m_next_prev_char_type = 0;
					m_next_prev_c = 0;
					goto end;
				}

				switch(char_type){
				case CT_SPACE:
					m_chop1 = i;
					i++;
					goto skip_space;

				case CT_HARD:
					if(chop_index > 0){
						m_chop1 = chop_index + 1;
						m_next_index = i + 1;
						m_next_chop0 = chop_index + 1;
						m_next_width = width - chop_width;
						m_next_prev_char_type = char_type;
						m_next_prev_c = c;
					}else{
						m_chop1 = i;
						m_next_index = i + 1;
						m_next_chop0 = i;
						m_next_width = w;
						m_next_prev_char_type = 0;
						m_next_prev_c = 0;
					}
					break;

				case CT_SOFT:
					if(m_chop0 + 1 < i){
						if(isOpen(m_str[i - 1]) || (isDoNotSplit(c) && !isDoNotSplit(m_str[i - 1]))){
							m_chop1 = i - 1;
							m_next_index = i - 1;
							m_next_chop0 = i - 1;
							m_next_width = 0;
							m_next_prev_char_type = 0;
							m_next_prev_c = 0;
							goto end;
						}
					}
					if(isClose(c)){
						//if(true){// debug
						if(m_max_width - (width - w) >= w / 2){
							i++;
							m_chop1 = i;
							goto skip_space;
						}
					}
					m_chop1 = i;
					m_next_index = i + 1;
					m_next_chop0 = i;
					m_next_width = w;
					m_next_prev_char_type = 0;
					m_next_prev_c = 0;
					break;
				}
				goto end;

			skip_space:
				for(; i < m_len; i++){
					c = m_str[i];
					if(c == '\r'){
						i++;
						if(i < m_len && m_str[i] == '\n')
							i++;
						break;
					}
					if(c == '\n'){
						i++;
						break;
					}
					if(!isSpace(c))
						break;
				}

				m_next_index = i;
				m_next_chop0 = i;
				m_next_width = 0;
				m_next_prev_char_type = 0;
				m_next_prev_c = 0;

			end:
				//m_line_count++;
				return true;
			}
		}

		private static Tokenizer s_token;
		private static readonly StringBuilder s_buf = new StringBuilder(256);// about

		public static int calcSize(bool tag, string str, out int line_count)
		{
			int width = 0;
			int max_width = 0;
			int line = 1;
			int len = str.Length;
			int i = 0;
			int prev_c = 0;

			for(; i < len; i++){
				int c = str[i];
				if(c == '<' && tag){
					if(i + 1 >= len)
						goto end;
					i = str.IndexOf('>', i + 1);
					if(i < 0)
						goto end;
					prev_c = 0;
					continue;
				}

				c = WordWrap_Unity.takeCharAndWidth(str, ref i, out int w, prev_c);
				prev_c = c;
				
				if(c == '\n'){
					if(width > max_width)
						max_width = width;
					width = 0;
					line++;
				}

				if(c == '\t'){
					width = (width / w + 1) * w;
				}else{
					width += w;
				}
			}

		end:
			line_count = line;
			return width > max_width ? width : max_width;
		}

		public static string wrap(bool tag, string text, int max_width, out int line_count)
		{
			s_token.init(tag, text, max_width);

			try{
				if(!s_token.nextChop()){
					line_count = s_token.m_line_count;
					return text;
				}

				StringBuilder sb = s_buf;
				sb.Length = 0;
				int copy_count;
				int lc = 0;

				for(;;){
					copy_count = s_token.m_chop1 - s_token.m_chop0;
					if(copy_count > 0)
						sb.Append(text, s_token.m_chop0, copy_count);

					if(!s_token.nextChop())
						break;
					sb.Append('\n');
					lc++;
				}

				copy_count = s_token.m_chop1 - s_token.m_chop0;
				if(copy_count > 0){
					sb.Append('\n');
					sb.Append(text, s_token.m_chop0, copy_count);
					lc++;
				}

				line_count = lc + s_token.m_line_count;
				return sb.ToString();

			}catch(ArgumentOutOfRangeException){
				line_count = 0;
				return text;// error. I don't care of this.
			}
		}
	}
}