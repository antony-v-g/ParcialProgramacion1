using System.ComponentModel.DataAnnotations;

namespace ParcialProgramacion1.Models;

public class SolicitudCredito
{
    public int Id { get; set; }

    [Required]
    public int ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    public decimal MontoSolicitado { get; set; }

    public DateTime FechaSolicitud { get; set; } = DateTime.Now;

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    [StringLength(300)]
    public string? MotivoRechazo { get; set; }

    public bool PuedeAprobarse()
    {
        if (Cliente == null) return false;

        return MontoSolicitado <= Cliente.IngresosMensuales * 5;
    }
}