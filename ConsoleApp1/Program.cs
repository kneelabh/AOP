using System;
using Autofac;
using Autofac.Extras.DynamicProxy;

namespace ConsoleApp1
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var conatiner = new ContainerBuilder();

            conatiner.RegisterType<Calculator>().As<ICalculator>().EnableInterfaceInterceptors()
                .InterceptedBy(typeof(CachingInterceptor));

            var resolver = conatiner.Build();

            resolver.Resolve<ICalculator>().Sum(2, 2);

            Console.ReadKey();
        }
    }
}