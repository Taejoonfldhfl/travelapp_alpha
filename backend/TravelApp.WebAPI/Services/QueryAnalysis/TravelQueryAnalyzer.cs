using TravelApp.WebAPI.Services.PlaceSearch;

namespace TravelApp.WebAPI.Services.QueryAnalysis
{
    // 사용자 질문을 LLM에 보내기 전에 규칙 기반으로 검색 의도/위치/카테고리를 뽑는다.
    // LLM이 검색 범위를 정하지 못하게 하려는 계층이라 LLM을 부르지 않는다(결정적이고 테스트 가능).
    //
    // 판단 순서:
    //  1) 장소 추천 요청이 아니면(일정/날씨 등만 묻고 장소 단서가 없으면) NonRecommendation
    //  2) 특정 가게/지점("~점", "X에 있는 Y", "Y 어때")이 있으면 SpecificPlace
    //  3) 지역/랜드마크 이름이 있으면 NamedLocation — "강남 근처"처럼 '근처'가 붙어도 GPS가 아니라 강남 기준
    //  4) 지역 없이 "근처/내 주변/여기"만 있으면 CurrentLocation
    //  5) 그 외 추천 요청은 GeneralRecommendation("일정 근처"면 RelativeLocation=NearSchedule)
    public static class TravelQueryAnalyzer
    {
        // 지명 사전: 접미어(역/동/구 등)만으로는 알아볼 수 없는, 여행에서 자주 쓰는 지역 이름.
        // 여기 없는 이름도 "X 근처/주변", "X에서" 형태이거나 지명 접미어로 끝나면 위치로 인식한다.
        private static readonly HashSet<string> KnownAreaNames = new(StringComparer.Ordinal)
        {
            "강남", "홍대", "신촌", "이태원", "명동", "광화문", "종로", "을지로", "여의도", "잠실", "성수", "건대",
            "압구정", "신사", "가로수길", "연남", "합정", "망원", "서촌", "북촌", "인사동", "삼청동", "익선동", "한남",
            "동대문", "남대문", "용산", "청담", "해운대", "광안리", "서면", "남포동", "기장", "전주", "경주", "강릉",
            "속초", "여수", "제주", "제주도", "서귀포", "부산", "서울", "대구", "대전", "광주", "인천", "수원", "판교"
        };

        // 이 접미어로 끝나는 3글자 이상 단어는 지명으로 본다(2글자는 '추천', '친구'처럼 오인이 많아 제외).
        private static readonly string[] LocationSuffixes =
        [
            "해수욕장", "대학교", "시장", "공원", "해변", "타워", "마을", "거리",
            "역", "동", "구", "군", "읍", "면", "로", "길", "궁", "섬", "항", "산", "봉", "천"
        ];

        private static readonly HashSet<string> NearWords = new(StringComparer.Ordinal)
        {
            "근처", "주변", "부근", "인근", "근방", "쪽", "앞", "옆"
        };

        private static readonly HashSet<string> UserRelativeWords = new(StringComparer.Ordinal)
        {
            "내", "제", "나", "우리", "저희", "여기", "이", "이곳", "현재", "지금", "현위치", "위치"
        };

        private static readonly HashSet<string> ScheduleRelativeWords = new(StringComparer.Ordinal)
        {
            "일정", "숙소"
        };

        private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
        {
            "추천", "추천해줘", "추천해", "추천좀", "추천해주세요", "알려줘", "알려", "알려주세요", "있어", "있을까", "있나", "있는",
            "뭐", "거", "곳", "데", "좀", "해줘", "줘", "이번", "여행", "오늘", "내일", "다른", "거기", "기준", "가는", "길",
            "들를만한", "싶어", "하고", "할", "만한", "좋은", "어디", "어때", "같이", "친구", "가족", "혼자", "가고", "싶은",
            "저기", "뭐가", "갈만한", "가볼만한", "볼만한", "괜찮은", "찾아줘",
            "날씨", "스케줄", "계획", "예산", "비용", "경비"
        };

        // 장소를 찾는다는 단서(카테고리 단어가 없어도 추천 요청으로 본다).
        private static readonly string[] PlaceCues = ["추천", "가볼", "갈만", "볼만", "어디", "곳", "데", "먹", "마실", "찾아", "맛집"];

        // 장소 단서 없이 이 단어들만 있으면 추천 요청이 아니다.
        private static readonly string[] NonRecommendationCues = ["일정", "스케줄", "계획", "날씨", "몇시", "몇 시", "예산", "비용", "경비"];

        // 카테고리 판단 단어. 2글자 이상은 포함 여부로, 1글자('술', '밥', '빵', '바')는 단어 전체가 같을 때만 본다.
        private static readonly (PlaceCategory Category, string[] Keywords)[] CategoryKeywords =
        [
            (PlaceCategory.Restaurant, ["맛집", "식당", "음식점", "밥집", "밥", "한식", "중식", "일식", "양식", "분식", "점심", "저녁", "아침",
                "브런치", "먹을", "먹고", "먹기", "먹거리", "먹으러", "파스타", "고기", "국밥", "냉면", "치킨", "피자", "초밥", "스시", "라멘", "우육면", "해장", "레스토랑", "뷔페", "요리"]),
            (PlaceCategory.Cafe, ["카페", "커피", "디저트", "베이커리", "빵집", "빵"]),
            (PlaceCategory.Bar, ["술집", "술", "이자카야", "포차", "호프", "펍", "와인바", "칵테일", "맥주", "한잔", "바"]),
            (PlaceCategory.Attraction, ["관광지", "관광", "명소", "볼만", "볼거리", "구경", "가볼", "박물관", "미술관", "전시", "유적", "궁궐", "산책"]),
            (PlaceCategory.Shopping, ["쇼핑", "백화점", "아울렛", "기념품", "쇼핑몰"]),
            (PlaceCategory.Hotel, ["호텔", "숙소", "숙박", "게스트하우스", "모텔", "펜션", "리조트"]),
        ];

        private static readonly string[] CuisineKeywords = ["한식", "중식", "일식", "양식", "분식"];

        // 업종보다 좁은 메뉴/품목. 이런 단어는 업종 필터로는 검색되지 않아 키워드 검색으로 보낸다.
        // (단어, 이 메뉴를 찾을 때의 장소 종류) — 사전에 없는 메뉴는 "X 먹고 싶어"의 X로도 뽑는다(FindMenuBeforeVerb).
        private static readonly (string Word, PlaceCategory Category)[] MenuKeywords =
        [
            ("파스타", PlaceCategory.Restaurant), ("피자", PlaceCategory.Restaurant), ("스시", PlaceCategory.Restaurant),
            ("초밥", PlaceCategory.Restaurant), ("라멘", PlaceCategory.Restaurant), ("돈까스", PlaceCategory.Restaurant),
            ("돈가스", PlaceCategory.Restaurant), ("삼겹살", PlaceCategory.Restaurant), ("갈비", PlaceCategory.Restaurant),
            ("곱창", PlaceCategory.Restaurant), ("족발", PlaceCategory.Restaurant), ("보쌈", PlaceCategory.Restaurant),
            ("국밥", PlaceCategory.Restaurant), ("냉면", PlaceCategory.Restaurant), ("칼국수", PlaceCategory.Restaurant),
            ("치킨", PlaceCategory.Restaurant), ("햄버거", PlaceCategory.Restaurant), ("버거", PlaceCategory.Restaurant),
            ("떡볶이", PlaceCategory.Restaurant), ("짜장면", PlaceCategory.Restaurant), ("짬뽕", PlaceCategory.Restaurant),
            ("마라탕", PlaceCategory.Restaurant), ("쌀국수", PlaceCategory.Restaurant), ("우육면", PlaceCategory.Restaurant),
            ("해산물", PlaceCategory.Restaurant), ("횟집", PlaceCategory.Restaurant), ("브런치", PlaceCategory.Restaurant),
            ("스테이크", PlaceCategory.Restaurant), ("오마카세", PlaceCategory.Restaurant),
            ("디저트", PlaceCategory.Cafe), ("빙수", PlaceCategory.Cafe), ("케이크", PlaceCategory.Cafe),
            ("와인", PlaceCategory.Bar), ("칵테일", PlaceCategory.Bar), ("막걸리", PlaceCategory.Bar), ("수제맥주", PlaceCategory.Bar),
        ];

        // "X 먹고 싶어"의 X로 나와도 메뉴가 아닌 일반적인 말(검색을 좁히지 못한다).
        private static readonly HashSet<string> GenericFoodWords = new(StringComparer.Ordinal)
        {
            "맛집", "식당", "음식점", "밥", "밥집", "점심", "저녁", "아침", "음식", "요리", "술", "커피", "한식", "중식", "일식", "양식", "분식"
        };

        private static readonly string[] PreferenceKeywords =
            ["분위기", "가성비", "저렴", "조용", "유명", "인기", "뷰", "야경", "데이트", "혼밥", "깔끔", "로컬", "노포", "24시", "주차"];

        // '~점'으로 끝나도 지점명이 아니라 업종인 단어들.
        private static readonly string[] NonBranchJeomWords =
            ["편의점", "음식점", "주점", "서점", "상점", "매점", "할인점", "전문점", "판매점", "대리점", "백화점", "가맹점"];

        // 흔한 조사. 먼저 긴 것부터 뗀다. '로/도/이/가/은/을'은 '종로', '제주도', '마을'처럼 지명의 일부인 경우가 많아
        // 기본으로는 떼지 않고, 떼었을 때 지명으로 인식되는 경우에만 쓴다(SecondaryParticles).
        private static readonly string[] PrimaryParticles = ["에서는", "에서", "에는", "으로", "에게", "이랑", "하고", "까지", "부터", "에", "의", "는", "를", "랑"];
        private static readonly string[] SecondaryParticles = ["은", "을", "이", "가", "도", "로"];

        public static TravelQueryAnalysis Analyze(string message)
        {
            string text = message ?? string.Empty;
            var rawTokens = Tokenize(text);
            var tokens = rawTokens.Select(StripPrimaryParticle).ToList();
            var usedIndexes = new HashSet<int>();

            string? specificPlace = FindSpecificPlace(rawTokens, tokens, usedIndexes, out string? locationFromPattern);
            string? location = locationFromPattern;
            var relative = RelativeLocationKind.None;

            // "X 근처/주변": X가 사용자 자신/일정이면 상대 위치, 아니면 X가 검색 중심 지명이다.
            for (int k = 0; k < tokens.Count; k++)
            {
                if (!NearWords.Contains(tokens[k]))
                {
                    continue;
                }

                usedIndexes.Add(k);

                if (k == 0)
                {
                    relative = RelativeLocationKind.NearUser;
                    continue;
                }

                string prev = tokens[k - 1];

                if (UserRelativeWords.Contains(prev))
                {
                    relative = RelativeLocationKind.NearUser;
                    usedIndexes.Add(k - 1);
                }
                else if (ScheduleRelativeWords.Contains(prev))
                {
                    relative = RelativeLocationKind.NearSchedule;
                    usedIndexes.Add(k - 1);
                }
                else if (location == null && !usedIndexes.Contains(k - 1) && IsLocationCandidateWord(prev))
                {
                    location = RecognizeLocation(prev) ?? prev;
                    usedIndexes.Add(k - 1);
                }
            }

            // "X에서" 또는 지명 사전/지명 접미어로 알아볼 수 있는 단어.
            for (int i = 0; i < tokens.Count && location == null; i++)
            {
                if (usedIndexes.Contains(i))
                {
                    continue;
                }

                bool hasLocativeParticle = rawTokens[i].EndsWith("에서", StringComparison.Ordinal) || rawTokens[i].EndsWith("에서는", StringComparison.Ordinal);
                string? recognized = RecognizeLocation(rawTokens[i]);

                if (recognized != null || (hasLocativeParticle && IsLocationCandidateWord(tokens[i])))
                {
                    location = recognized ?? tokens[i];
                    usedIndexes.Add(i);
                }
            }

            // 명시적인 "현재 위치", "여기" 표현(근처라는 말이 없어도).
            if (relative == RelativeLocationKind.None &&
                (text.Contains("현재 위치") || text.Contains("현재위치") || text.Contains("지금 위치") || tokens.Contains("여기")))
            {
                relative = RelativeLocationKind.NearUser;
            }

            var keywords = new List<string>();
            var category = DetectCategory(rawTokens, tokens, usedIndexes, keywords);
            string? cuisine = CuisineKeywords.FirstOrDefault(c => keywords.Any(k => k.Contains(c, StringComparison.Ordinal)));
            var (menu, menuCategory) = FindMenu(tokens, usedIndexes);

            // 메뉴만 있고 업종 단어가 없으면("마라탕 먹고 싶어"는 '먹고'로 음식점이 잡히지만, "디저트"만 있으면 없음) 메뉴의 종류를 쓴다.
            if (menu != null && category == PlaceCategory.Other)
            {
                category = menuCategory;
            }
            string? preference = DetectPreference(tokens, usedIndexes);
            bool mentionsToday = tokens.Contains("오늘");

            var intent = DecideIntent(text, category, specificPlace, location, relative);

            return new TravelQueryAnalysis(
                intent,
                location,
                category,
                keywords,
                specificPlace,
                relative,
                cuisine,
                preference,
                mentionsToday,
                menu);
        }

        // 메뉴 사전에 있는 단어를 먼저 찾고, 없으면 "X 먹고/먹을/마시러"의 X를 메뉴로 본다(지명·일반 음식 표현은 제외).
        private static (string? Menu, PlaceCategory Category) FindMenu(List<string> tokens, HashSet<int> usedIndexes)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                if (usedIndexes.Contains(i))
                {
                    continue;
                }

                foreach (var (word, category) in MenuKeywords)
                {
                    if (tokens[i].Contains(word, StringComparison.Ordinal))
                    {
                        return (word, category);
                    }
                }
            }

            for (int i = 1; i < tokens.Count; i++)
            {
                bool isEatOrDrinkVerb = tokens[i].StartsWith("먹", StringComparison.Ordinal) || tokens[i].StartsWith("마시", StringComparison.Ordinal);
                string candidate = tokens[i - 1];

                if (isEatOrDrinkVerb && !usedIndexes.Contains(i - 1) && candidate.Length >= 2 &&
                    IsNameWord(candidate) && !GenericFoodWords.Contains(candidate) && RecognizeLocation(candidate) == null)
                {
                    return (candidate, tokens[i].StartsWith("마시", StringComparison.Ordinal) ? PlaceCategory.Bar : PlaceCategory.Restaurant);
                }
            }

            return (null, PlaceCategory.Other);
        }

        private static TravelQueryIntent DecideIntent(
            string text, PlaceCategory category, string? specificPlace, string? location, RelativeLocationKind relative)
        {
            bool hasPlaceCue = PlaceCues.Any(c => text.Contains(c, StringComparison.Ordinal));
            bool hasNonRecommendationCue = NonRecommendationCues.Any(c => text.Contains(c, StringComparison.Ordinal));

            if (hasNonRecommendationCue && !hasPlaceCue && category == PlaceCategory.Other &&
                specificPlace == null && location == null && relative != RelativeLocationKind.NearUser)
            {
                return TravelQueryIntent.NonRecommendation;
            }

            if (specificPlace != null)
            {
                return TravelQueryIntent.SpecificPlace;
            }

            if (location != null)
            {
                return TravelQueryIntent.NamedLocation;
            }

            if (relative == RelativeLocationKind.NearUser)
            {
                return TravelQueryIntent.CurrentLocation;
            }

            return TravelQueryIntent.GeneralRecommendation;
        }

        // 특정 가게/지점: "브랜드 지점점"(예: 스타벅스 강남역점), "X에 있는 Y", "Y 어때".
        // "X에 있는 Y"의 X는 그 가게를 찾을 때의 지역 단서로 함께 돌려준다.
        private static string? FindSpecificPlace(
            List<string> rawTokens, List<string> tokens, HashSet<int> usedIndexes, out string? locationHint)
        {
            locationHint = null;

            for (int i = 0; i < tokens.Count; i++)
            {
                if (IsBranchToken(tokens[i]))
                {
                    usedIndexes.Add(i);
                    bool hasBrand = i > 0 && IsNameWord(tokens[i - 1]) && RecognizeLocation(tokens[i - 1]) == null;

                    if (hasBrand)
                    {
                        usedIndexes.Add(i - 1);
                        return $"{tokens[i - 1]} {tokens[i]}";
                    }

                    return tokens[i];
                }
            }

            for (int i = 0; i + 2 < rawTokens.Count; i++)
            {
                if (rawTokens[i].EndsWith("에", StringComparison.Ordinal) && tokens[i + 1] == "있는")
                {
                    string place = StripPrimaryParticle(tokens[i + 2]);

                    if (IsNameWord(place) && FindCategory(place) == null)
                    {
                        locationHint = RecognizeLocation(rawTokens[i]) ?? tokens[i];
                        usedIndexes.UnionWith([i, i + 1, i + 2]);
                        return place;
                    }
                }
            }

            for (int j = 1; j < tokens.Count; j++)
            {
                if (tokens[j] is "어때" or "어떄" or "어떤가요" or "어때요")
                {
                    string place = tokens[j - 1];

                    if (IsNameWord(place) && FindCategory(place) == null && RecognizeLocation(place) == null)
                    {
                        usedIndexes.Add(j - 1);
                        return place;
                    }
                }
            }

            return null;
        }

        private static PlaceCategory DetectCategory(
            List<string> rawTokens, List<string> tokens, HashSet<int> usedIndexes, List<string> keywords)
        {
            var detected = PlaceCategory.Other;

            // 문장 끝쪽 명사가 찾는 대상인 경우가 많아("숙소 근처 맛집") 마지막으로 매칭된 카테고리를 쓴다.
            for (int i = 0; i < tokens.Count; i++)
            {
                if (usedIndexes.Contains(i))
                {
                    continue;
                }

                var category = FindCategory(tokens[i]) ?? FindCategory(rawTokens[i]);

                if (category != null)
                {
                    detected = category.Value;
                    keywords.Add(tokens[i]);
                }
            }

            return detected;
        }

        private static PlaceCategory? FindCategory(string token)
        {
            foreach (var (category, words) in CategoryKeywords)
            {
                foreach (var word in words)
                {
                    bool matches = word.Length == 1
                        ? token == word
                        : token.Contains(word, StringComparison.Ordinal);

                    if (matches)
                    {
                        return category;
                    }
                }
            }

            return null;
        }

        private static string? DetectPreference(List<string> tokens, HashSet<int> usedIndexes)
        {
            var preferences = tokens
                .Where((t, i) => !usedIndexes.Contains(i) && PreferenceKeywords.Any(p => t.Contains(p, StringComparison.Ordinal)))
                .ToList();

            return preferences.Count > 0 ? string.Join(" ", preferences) : null;
        }

        // 지명 사전 또는 지명 접미어로 알아볼 수 있으면 그 지명(조사를 뗀 형태)을 돌려준다.
        private static string? RecognizeLocation(string rawToken)
        {
            foreach (var variant in Variants(rawToken))
            {
                if (KnownAreaNames.Contains(variant))
                {
                    return variant;
                }
            }

            foreach (var variant in Variants(rawToken))
            {
                if (variant.Length >= 3 && IsNameWord(variant) && FindCategory(variant) == null &&
                    !IsBranchToken(variant) && LocationSuffixes.Any(s => variant.EndsWith(s, StringComparison.Ordinal)))
                {
                    return variant;
                }
            }

            return null;
        }

        // "X 근처"/"X에서"의 X로 쓸 수 있는 단어인지(사전에 없는 랜드마크도 받아들인다).
        private static bool IsLocationCandidateWord(string token) =>
            token.Length >= 2 && IsNameWord(token) && FindCategory(token) == null && !IsBranchToken(token);

        private static bool IsNameWord(string token) =>
            token.Length > 0 &&
            !StopWords.Contains(token) &&
            !NearWords.Contains(token) &&
            !UserRelativeWords.Contains(token) &&
            !ScheduleRelativeWords.Contains(token);

        private static bool IsBranchToken(string token) =>
            token.Length >= 3 &&
            token.EndsWith("점", StringComparison.Ordinal) &&
            !NonBranchJeomWords.Any(w => token.EndsWith(w, StringComparison.Ordinal));

        private static IEnumerable<string> Variants(string rawToken)
        {
            string primary = StripPrimaryParticle(rawToken);
            yield return rawToken;

            if (primary != rawToken)
            {
                yield return primary;
            }

            foreach (var particle in SecondaryParticles)
            {
                if (primary.Length > particle.Length + 1 && primary.EndsWith(particle, StringComparison.Ordinal))
                {
                    yield return primary[..^particle.Length];
                }
            }
        }

        private static string StripPrimaryParticle(string token)
        {
            // '있는', '만한'처럼 그 자체로 의미 있는 단어는 조사로 끝나 보여도 떼지 않는다.
            if (StopWords.Contains(token))
            {
                return token;
            }

            foreach (var particle in PrimaryParticles)
            {
                if (token.Length > particle.Length && token.EndsWith(particle, StringComparison.Ordinal))
                {
                    return token[..^particle.Length];
                }
            }

            return token;
        }

        private static List<string> Tokenize(string text)
        {
            var cleaned = new string(text.Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());
            return cleaned
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant())
                .ToList();
        }
    }
}
