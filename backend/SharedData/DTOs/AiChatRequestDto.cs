using System.ComponentModel.DataAnnotations;

namespace SharedData.DTOs
{
    public class AiChatRequestDto
    {
        [Required(ErrorMessage = "메시지를 입력해주세요.")]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;
    }
}
