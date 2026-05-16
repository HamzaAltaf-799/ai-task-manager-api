using AITaskManager.API.Common;
using AITaskManager.API.DTOs.Auth;
using AITaskManager.API.Interfaces;
using AITaskManager.API.Validators;
using Microsoft.AspNetCore.Mvc;

namespace AITaskManager.API.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService auth, ILogger<AuthController> logger)
    {
        _auth   = auth;
        _logger = logger;
    }

    /// <summary>Register a new user account.</summary>
    /// <response code="201">Account created, returns JWT token.</response>
    /// <response code="400">Validation errors.</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var errors = RequestValidator.ValidateRegister(request);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", errors: errors));

        var result = await _auth.RegisterAsync(request);
        if (result is null)
            return Conflict(ApiResponse<object>.Fail("An account with this email already exists.", 409));

        return StatusCode(201, ApiResponse<AuthResponse>.Created(result, "Account created successfully."));
    }

    /// <summary>Authenticate with email and password, receive a JWT token.</summary>
    /// <response code="200">Login successful.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var errors = RequestValidator.Validate(request);
        if (errors.Count > 0)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", errors: errors));

        var result = await _auth.LoginAsync(request);
        if (result is null)
            return Unauthorized(ApiResponse<object>.Fail("Invalid email or password.", 401));

        return Ok(ApiResponse<AuthResponse>.Ok(result, "Login successful."));
    }
}
