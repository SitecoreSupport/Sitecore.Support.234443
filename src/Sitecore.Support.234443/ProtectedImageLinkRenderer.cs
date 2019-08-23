namespace Sitecore.Support.Pipelines.RenderField
{
    using Sitecore.Configuration;
    using Sitecore.Diagnostics;
    using Sitecore.Pipelines.RenderField;
    using Sitecore.Resources.Media;
    using System;
    using System.Text;

    public class ProtectedImageLinkRenderer
    {
        private readonly char[] quotes = new char[] { '\'', '"' };

        protected bool CheckReferenceForParams(string renderedText, int tagStartIndex) =>
            this.CheckReferenceForParams(renderedText, tagStartIndex, "img", "src");

        protected bool CheckReferenceForParams(string renderedText, int tagStartIndex, string tagName, string urlAttribute)
        {
            Assert.ArgumentNotNull(renderedText, "renderedText");
            Assert.ArgumentNotNull(tagName, "tagName");
            Assert.ArgumentNotNull(urlAttribute, "urlAttribute");
            int startIndex = renderedText.IndexOf(urlAttribute, tagStartIndex, StringComparison.OrdinalIgnoreCase) + 3;
            startIndex = renderedText.IndexOfAny(this.quotes, startIndex) + 1;
            int num2 = renderedText.IndexOfAny(this.quotes, startIndex);
            int num3 = renderedText.IndexOf('?', startIndex, num2 - startIndex);
            return ((num3 >= 0) ? this.ContainsUnsafeParametersInQuery(renderedText.Substring(num3, num2 - num3).Replace("&amp;", "&")) : false);
        }

        protected virtual bool ContainsUnsafeParametersInQuery(string urlParameters) =>
            !HashingUtils.IsSafeUrl(urlParameters);

        protected virtual string GetProtectedUrl(string url)
        {
            Assert.IsNotNull(url, "url");
            return HashingUtils.ProtectAssetUrl(url);
        }

        protected string HashImageReferences(string renderedText)
        {
            Assert.ArgumentNotNull(renderedText, "renderedText");
            return this.HashReferences(renderedText, "img", "src");
        }

        protected string HashLinkReferences(string renderedText)
        {
            Assert.ArgumentNotNull(renderedText, "renderedText");
            return this.HashReferences(renderedText, "a", "href");
        }

        protected string HashReferences(string renderedText)
        {
            Assert.ArgumentNotNull(renderedText, "renderedText");
            return this.HashLinkReferences(this.HashImageReferences(renderedText));
        }

        protected string HashReferences(string renderedText, string tagName, string urlAttribute)
        {
            Assert.ArgumentNotNull(renderedText, "renderedText");
            Assert.ArgumentNotNull(tagName, "tagName");
            Assert.ArgumentNotNull(urlAttribute, "urlAttribute");
            string str = $"<{tagName}";
            if (renderedText.IndexOf($"{str} ", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return renderedText;
            }
            int startIndex = 0;
            bool flag = false;
            while (true)
            {
                if ((startIndex < renderedText.Length) && !flag)
                {
                    int tagStartIndex = renderedText.IndexOf(str, startIndex, StringComparison.OrdinalIgnoreCase);
                    if (tagStartIndex >= 0)
                    {
                        flag = this.CheckReferenceForParams(renderedText, tagStartIndex, tagName, urlAttribute);
                        startIndex = renderedText.IndexOf(">", tagStartIndex, StringComparison.OrdinalIgnoreCase) + 1;
                        continue;
                    }
                }
                if (!flag)
                {
                    return renderedText;
                }
                startIndex = 0;
                StringBuilder builder = new StringBuilder(renderedText.Length + 0x80);
                while (startIndex < renderedText.Length)
                {
                    int num4 = renderedText.IndexOf(str, startIndex, StringComparison.OrdinalIgnoreCase);
                    if (num4 <= -1)
                    {
                        builder.Append(renderedText.Substring(startIndex, renderedText.Length - startIndex));
                        startIndex = 0x7fffffff;
                        continue;
                    }
                    int num5 = renderedText.IndexOf(">", num4, StringComparison.OrdinalIgnoreCase) + 1;
                    builder.Append(renderedText.Substring(startIndex, num4 - startIndex));
                    string tagHtml = renderedText.Substring(num4, num5 - num4);
                    builder.Append(this.ReplaceReference(tagHtml, urlAttribute));
                    startIndex = num5;
                }
                return builder.ToString();
            }
        }

        public void Process(RenderFieldArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (Settings.Media.RequestProtection.Enabled && !args.FieldTypeKey.StartsWith("__"))
            {
                args.Result.FirstPart = this.HashReferences(args.Result.FirstPart);
                args.Result.LastPart = this.HashReferences(args.Result.LastPart);
            }
        }

        #region Fixed version

        private string ReplaceReference(string tagHtml, string urlAttribute)
        {
            Assert.ArgumentNotNull(tagHtml, "tagHtml");
            Assert.ArgumentNotNull(urlAttribute, "urlAttribute");
            bool flag = true;
            string str = tagHtml;

            int startIndex = str.IndexOf(urlAttribute, StringComparison.OrdinalIgnoreCase) + 3;
            startIndex = str.IndexOfAny(this.quotes, startIndex) + 1;
            int num2 = str.IndexOfAny(this.quotes, startIndex);
            string url = str.Substring(startIndex, num2 - startIndex);

            // We encode and decode the ampersand (&) for the src (url) attribute. 
            // The Alt attribute is no longer decoded.

            if (!url.Contains("?"))
            {
                return tagHtml;
            }

            if (url.Contains("&amp;"))
            {
                url = url.Replace("&amp;", "&");
            }
            else if (url.Contains("&"))
            {
                flag = false;
            }

            url = this.GetProtectedUrl(url);

            if (flag)
            {
                url = url.Replace("&", "&amp;");
            }
    
            return str.Substring(0, startIndex) + url + str.Substring(num2, str.Length - num2);
        }

        #endregion
    }
}