using System.Threading.Tasks;

namespace ConsoleApp1
{
    public class Calculator : ICalculator
    {
        /// <inheritdoc />
        public int Sum(int x, int y)
        {
            return 0;
        }

        /// <inheritdoc />
        public Task<int> Sum(double x, double y)
        {
            return null;
        }
    }
}