using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using ParcialProgramacion1.Data;
using ParcialProgramacion1.Models;

namespace ParcialProgramacion1.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;

    public AnalistaController(ApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        var solicitudes = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        return View(solicitudes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
            return NotFound();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = "No se puede procesar una solicitud que ya fue aprobada o rechazada.";
            return RedirectToAction(nameof(Index));
        }

        if (solicitud.Cliente == null)
        {
            TempData["Error"] = "La solicitud no tiene cliente asociado.";
            return RedirectToAction(nameof(Index));
        }

        if (solicitud.MontoSolicitado > solicitud.Cliente.IngresosMensuales * 5)
        {
            TempData["Error"] = "No se puede aprobar porque el monto excede 5 veces los ingresos mensuales.";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Aprobado;
        solicitud.MotivoRechazo = null;

        await _context.SaveChangesAsync();
        await InvalidarCacheUsuario(solicitud.Cliente.UsuarioId);

        TempData["Exito"] = "Solicitud aprobada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string motivoRechazo)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
            return NotFound();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = "No se puede procesar una solicitud que ya fue aprobada o rechazada.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            TempData["Error"] = "El motivo de rechazo es obligatorio.";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivoRechazo.Trim();

        await _context.SaveChangesAsync();

        if (solicitud.Cliente != null)
            await InvalidarCacheUsuario(solicitud.Cliente.UsuarioId);

        TempData["Exito"] = "Solicitud rechazada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    private async Task InvalidarCacheUsuario(string usuarioId)
    {
        var cacheKey = $"solicitudes_usuario_{usuarioId}";
        await _cache.RemoveAsync(cacheKey);
    }
}