using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using SharedData.DTOs;
using SharedData.Models;
using TravelApp.WebAPI.Data;
using Microsoft.AspNetCore.Authorization;



namespace TravelApp.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public UserController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // 1. 회원가입
        [HttpPost("register")]
        public async Task<ActionResult<UserResponseDto>> Register(UserRegisterDto request)
        {
            // 이메일 중복 체크
            if (await _context.Users.AnyAsync(u => u.Email == request.Email)) 
            {
                return BadRequest("이미 존재하는 계정입니다.");
            }

            // 비밀번호 암호화
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                Email = request.Email,
                PasswordHash = passwordHash,
                Nickname = request.Nickname
            };

            // DB에 사용자 정보 저장
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            string token = CreateToken(user);       // 회원가입 후 바로 로그인 되게 하려면 사용 아니면 지우는걸로

            return Ok(new UserResponseDto
            {
                Email = user.Email,
                Nickname = user.Nickname,
                Token = token       
            });
        }

        // 2. 로그인
        [HttpPost("login")]
        public async Task<ActionResult<UserResponseDto>> Login(UserLoginDto request)
        {
            // 이메일로 사용자 조회
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized("이메일 또는 비밀번호가 일치하지 않습니다.");
            }

            string token = CreateToken(user);
            return Ok(new UserResponseDto
            {
                Email = user.Email,
                Nickname = user.Nickname,
                Token = token // JWT 토큰을 생성하여 반환
            });
        }

        // 3. 정보 조회
        [HttpGet("me")]
        [Authorize]
        public IActionResult GetMyInfo()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var nickname = User.FindFirstValue(ClaimTypes.Name);

            return Ok(new
            {   
                message = "인증 성공",
                email = email,
                nickname = nickname
            });
        }

        private string CreateToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Nickname)
            };

            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_configuration.GetSection("Jwt:Key").Value!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration.GetSection("Jwt:Issuer").Value,
                audience: _configuration.GetSection("Jwt:Audience").Value,
                claims: claims,
                expires: DateTime.Now.AddDays(1),   // 토큰 유효기간
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
