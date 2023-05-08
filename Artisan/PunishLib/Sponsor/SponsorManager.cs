using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PunishLib.Sponsor
{
    public static class SponsorManager
    {
        public static SponsorInfo SponsorInfo { get; private set; }

        public static void SetSponsorInfo(string WebsiteURL)
        {
            if (string.IsNullOrEmpty(WebsiteURL))
            {
                throw new ArgumentException($"'{nameof(WebsiteURL)}' cannot be null or empty.", nameof(WebsiteURL));
            }

            SponsorInfo = new()
            {
                WebsiteURL = WebsiteURL,
            };
        }

        public static void SetSponsorInfo(SponsorInfo info)
        {
            SponsorInfo = info ?? throw new ArgumentNullException(nameof(info));
        }
    }
}
