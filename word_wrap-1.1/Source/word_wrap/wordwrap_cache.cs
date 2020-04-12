using System;
using System.Collections.Generic;


namespace word_wrap
{
	class CacheData {
		public int line_count;
		public int width;
		public string str;
		public int wrap_line_count;
		public int wrap_width;
		public string wrap_str;
	}

	class CacheItem {
		static Stack<CacheData> s_pool = new Stack<CacheData>(128);

		static CacheData dataGet()
		{
			if(s_pool.Count > 0)
				return s_pool.Pop();

			return new CacheData();
		}

		static void dataPut(CacheData data)
		{
			data.line_count = 0;
			data.width = 0;
			data.str = null;
			data.wrap_line_count = 0;
			data.wrap_width = 0;
			data.wrap_str = null;
			
			s_pool.Push(data);
		}

		public CacheItem m_prev;
		public CacheItem m_next;

		public string m_key;
		public bool m_mark;
		readonly CacheData[] m_data = new CacheData[3];


		public void cleanValue()
		{
			m_key = null;
			for(int i = 0; i < 3; i++){
				if(m_data[i] != null){
					dataPut(m_data[i]);
					m_data[i] = null;
				}
			}
		}

		public CacheData getData(int index)
		{
			CacheData data = m_data[index];
			if(data == null){
				data = dataGet();
				m_data[index] = data;
			}

			return data;
		}
	}

	class Cache {
		static CacheItem s_item_pool;
		static CacheItem getItem()
		{
			if(s_item_pool == null){
				return new CacheItem();
			}

			CacheItem item = s_item_pool;
			s_item_pool = item.m_next;
			item.m_prev = null;
			item.m_next = null;
			return item;
		}
			
		Dictionary<string, CacheItem> m_cache;
		CacheItem m_used;
		CacheItem m_unused;
		bool m_mark;

		public Cache()
		{
			m_cache = new Dictionary<string, CacheItem>();
		}

		public void clean()
		{
			// discard unused-caches
			{
				CacheItem head = m_unused;
				if(head != null){
					CacheItem tail = head;
					for(;;){
						m_cache.Remove(tail.m_key);
						tail.cleanValue();
						if(tail.m_next == null)
							break;
						tail = tail.m_next;
					}
					tail.m_next = s_item_pool;
					s_item_pool = head;
				}
			}

			// swap & clear
			m_unused = m_used;
			m_used = null;
			m_mark = !m_mark;
		}

		private void prependToUsed(CacheItem item)
		{
			if(m_used == null){
				// first time
				m_used = item;
				return;
			}

			m_used.m_prev = item;
			item.m_next = m_used;
			m_used = item;
		}

		private void removeFromUnused(CacheItem item)
		{
			var n = item.m_next;
			var p = item.m_prev;
			item.m_next = null;
			item.m_prev = null;

			if(p != null)
				p.m_next = n;
			if(n != null)
				n.m_prev = p;

			if(item == m_unused)
				m_unused = n;
		}

		public CacheData getData(string key, int font_index)
		{
			CacheItem value;

			if(m_cache.TryGetValue(key, out value)){
				if(value.m_mark != m_mark){
					value.m_mark = m_mark;
					removeFromUnused(value);
					prependToUsed(value);
				}
			}else{
				value = getItem();

				value.m_key = key;
				value.m_mark = m_mark;
				m_cache.Add(key, value);

				prependToUsed(value);
			}
			
			return value.getData(font_index);
		}
	}
}
