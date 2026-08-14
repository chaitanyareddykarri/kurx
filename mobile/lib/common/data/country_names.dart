/// English country names keyed by ISO 3166-1 alpha-2 (Phase 6). The phone picker lists every country
/// `phone_numbers_parser` knows (dial codes + flags are derived at runtime); this supplies friendly
/// names for the common ones and falls back to the ISO code for micro-territories. Web/admin get names
/// from `Intl.DisplayNames`; Flutter has no built-in equivalent, hence this table.
const Map<String, String> countryNames = {
  'IN': 'India', 'US': 'United States', 'GB': 'United Kingdom', 'AE': 'United Arab Emirates',
  'SG': 'Singapore', 'CA': 'Canada', 'AU': 'Australia', 'NZ': 'New Zealand', 'IE': 'Ireland',
  'DE': 'Germany', 'FR': 'France', 'IT': 'Italy', 'ES': 'Spain', 'PT': 'Portugal', 'NL': 'Netherlands',
  'BE': 'Belgium', 'CH': 'Switzerland', 'AT': 'Austria', 'SE': 'Sweden', 'NO': 'Norway', 'DK': 'Denmark',
  'FI': 'Finland', 'PL': 'Poland', 'CZ': 'Czechia', 'GR': 'Greece', 'RO': 'Romania', 'HU': 'Hungary',
  'UA': 'Ukraine', 'RU': 'Russia', 'TR': 'Türkiye', 'IL': 'Israel', 'SA': 'Saudi Arabia', 'QA': 'Qatar',
  'KW': 'Kuwait', 'BH': 'Bahrain', 'OM': 'Oman', 'JO': 'Jordan', 'LB': 'Lebanon', 'EG': 'Egypt',
  'ZA': 'South Africa', 'NG': 'Nigeria', 'KE': 'Kenya', 'GH': 'Ghana', 'TZ': 'Tanzania', 'UG': 'Uganda',
  'ET': 'Ethiopia', 'MA': 'Morocco', 'DZ': 'Algeria', 'TN': 'Tunisia',
  'CN': 'China', 'JP': 'Japan', 'KR': 'South Korea', 'HK': 'Hong Kong', 'TW': 'Taiwan', 'MO': 'Macau',
  'PK': 'Pakistan', 'BD': 'Bangladesh', 'LK': 'Sri Lanka', 'NP': 'Nepal', 'BT': 'Bhutan', 'MV': 'Maldives',
  'AF': 'Afghanistan', 'ID': 'Indonesia', 'MY': 'Malaysia', 'TH': 'Thailand', 'VN': 'Vietnam',
  'PH': 'Philippines', 'MM': 'Myanmar', 'KH': 'Cambodia', 'LA': 'Laos', 'BN': 'Brunei',
  'BR': 'Brazil', 'MX': 'Mexico', 'AR': 'Argentina', 'CL': 'Chile', 'CO': 'Colombia', 'PE': 'Peru',
  'VE': 'Venezuela', 'EC': 'Ecuador', 'UY': 'Uruguay', 'PY': 'Paraguay', 'BO': 'Bolivia',
  'CR': 'Costa Rica', 'PA': 'Panama', 'DO': 'Dominican Republic', 'GT': 'Guatemala', 'JM': 'Jamaica',
  'IS': 'Iceland', 'LU': 'Luxembourg', 'MT': 'Malta', 'CY': 'Cyprus', 'HR': 'Croatia', 'RS': 'Serbia',
  'BG': 'Bulgaria', 'SK': 'Slovakia', 'SI': 'Slovenia', 'LT': 'Lithuania', 'LV': 'Latvia', 'EE': 'Estonia',
  'IR': 'Iran', 'IQ': 'Iraq', 'SY': 'Syria', 'YE': 'Yemen', 'KZ': 'Kazakhstan', 'UZ': 'Uzbekistan',
  'AZ': 'Azerbaijan', 'GE': 'Georgia', 'AM': 'Armenia', 'FJ': 'Fiji', 'PG': 'Papua New Guinea',
  'MU': 'Mauritius', 'SC': 'Seychelles', 'ZM': 'Zambia', 'ZW': 'Zimbabwe', 'BW': 'Botswana',
  'MZ': 'Mozambique', 'AO': 'Angola', 'SN': 'Senegal', 'CI': "Côte d'Ivoire", 'CM': 'Cameroon',
};
