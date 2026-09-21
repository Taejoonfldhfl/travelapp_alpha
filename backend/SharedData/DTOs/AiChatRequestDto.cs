using System.ComponentModel.DataAnnotations;

namespace SharedData.DTOs
{
    public class AiChatRequestDto
    {
        [Required(ErrorMessage = "메시지를 입력해주세요.")]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        // "근처/주변" 표현에 대응하기 위한 사용자의 현재 GPS 좌표.
        // 위치 권한이 없거나 GPS를 가져오지 못한 경우 프론트에서 null로 보낸다.
        public double? CurrentLatitude { get; set; }
        public double? CurrentLongitude { get; set; }
    }
}
