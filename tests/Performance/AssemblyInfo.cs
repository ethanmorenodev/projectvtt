using Xunit;

// Las pruebas de rendimiento arrancan su propia API en el puerto fijo 5098:
// dos fixtures simultáneas competirían por el puerto. Además, la carga de la
// rejilla no debe contaminar el p95 del resto (PMB-4.9).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
