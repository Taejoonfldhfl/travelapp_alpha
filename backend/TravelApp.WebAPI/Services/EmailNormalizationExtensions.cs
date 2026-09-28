using SharedData.Models;

namespace TravelApp.WebAPI.Services
{
    // 이메일은 대소문자/앞뒤 공백을 구분하지 않는다. 새로 저장할 때는 NormalizeEmail()로 정규화한 값을 저장하고,
    // 비교할 때는 WhereEmailMatches()로 입력값과 DB 값 양쪽을 정규화해 비교한다 — 정규화 전에 대소문자가 섞인 채로
    // 저장된 기존 계정도 로그인/초대가 되게 하기 위해서다(기존 데이터 일괄 변환은 별도 작업).
    public static class EmailNormalizationExtensions
    {
        public static string NormalizeEmail(this string? email) =>
            (email ?? string.Empty).Trim().ToLowerInvariant();

        // DB 쪽 정규화는 SQL로 번역돼야 해서 NormalizeEmail() 대신 Trim().ToLower()를 쓴다
        // (Npgsql: lower(btrim(email))). 이메일은 ASCII라 ToLowerInvariant와 결과가 같다.
        public static IQueryable<User> WhereEmailMatches(this IQueryable<User> users, string? email)
        {
            string normalized = email.NormalizeEmail();
            return users.Where(u => u.Email.Trim().ToLower() == normalized);
        }
    }
}
