using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tern.Api.Data;
using Tern.Api.Dtos;

namespace Tern.Api.Controllers;

[ApiController, Route("api/skills")]
public class SkillsController : ControllerBase
{
    private readonly AppDbContext _db;
    public SkillsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<List<SkillDto>> List()
        => await _db.Skills.AsNoTracking().OrderBy(s => s.Name).Select(s => new SkillDto(s.Id, s.Name)).ToListAsync();
}
