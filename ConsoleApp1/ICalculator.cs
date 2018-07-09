using System.Threading.Tasks;

namespace ConsoleApp1
{
    public interface ICalculator
    {
        int Sum(int x, int y);

        Task<int> Sum(double x, double y);
    }
}