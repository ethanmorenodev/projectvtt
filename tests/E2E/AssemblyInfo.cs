using Xunit;

// Las pruebas E2E levantan sus propios servidores en puertos fijos
// (4173/5099): las clases deben ejecutarse en serie para no competir por
// ellos ni por la base de datos de la factoría.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
