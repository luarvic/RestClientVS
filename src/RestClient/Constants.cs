﻿namespace RestClient
{
    public class Constants
    {
        public const char CommentChar = '#';
        public const string MarketplaceId = "MadsKristensen.RestClient";
        public const string RegexReferenceDelimiter = ":";
        public const string RegexReference = @$"{{{{(?<object>[\w{RegexReferenceDelimiter}-]+)}}}}";
    }
}
