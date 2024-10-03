using HealthMonitorApp.Dtos;
using HealthMonitorApp.Models;

namespace HealthMonitorApp.Interfaces;

public interface ICurlCommandService
{
    Task SaveCurlCommandDto(CurlCommandDto curlCommandDto);
    Task SaveAllCurlCommandDtos(List<CurlCommandDto> curlCommandDtos);
}