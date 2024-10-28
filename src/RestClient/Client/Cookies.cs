using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RestClient.Client;

public class Cookies : IEnumerable<Cookies.Cookie>
{
    public class Cookie: IEqualityComparer<Cookie>
    {
        public string Host { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }

        public Cookie(string host, string key, string value)
        {
            Host = host;
            Key = key;
            Value = value;
        }

        public bool Equals(Cookie x, Cookie y)
        {
            return GetHashCode(x) == GetHashCode(y);
        }

        public int GetHashCode(Cookie obj)
        {
            return $"{obj.Host} {obj.Key}".GetHashCode();
        }
    }

    private readonly static Cookies _instance = new();
    private readonly HashSet<Cookie> _items = new();

    private Cookies()
    {
    }

    public static Cookies GetInstance()
    {
        return _instance;
    }

    public IEnumerator<Cookie> GetEnumerator()
    {
        foreach (var cookie in _instance._items)
        {
            yield return cookie;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Set(string host, string key, string value)
    {
        Remove(host, key);
        var cookie = new Cookie(host, key, value);
        return _items.Add(cookie);
    }

    public Cookie? Get(string host, string key)
    {
        return _items.FirstOrDefault(x => x.Host == host && x.Key == key);
    }

    public int Remove(string host, string key)
    {
        return _items.RemoveWhere(x => x.Host == host && x.Key == key);
    }
}
