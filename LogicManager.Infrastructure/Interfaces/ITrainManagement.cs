using LogicManager.Domain.Entities;

namespace LogicManager.Infrastructure.Interfaces
{
    public interface ITrainManagement
    {

        bool IsRouteActive { get; }
      
        List<Station> FilterAndCalculateSkipStations(List<Station> stations);


        Task InitializeFirstStation();

        //Task ProcessTakoValueAsync(double takoValue);

        void UpdateDisplays();


        Task CheckStationArrivalAsync(double ZeroSpeed,bool AllDoorsReleased);

        Task MoveToNextStationAsync();

        Task CompleteRouteAsync();

        int GetDistanceToNextStation(Station station);

        bool IsLastStation();

        int CalculateDistance(int takoValue);
        Task ReadAndProcessTakoAsync();
       
    }
}
