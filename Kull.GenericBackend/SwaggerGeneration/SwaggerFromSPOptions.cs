using System;
using System.Collections.Generic;
using System.Text;

namespace Kull.GenericBackend.SwaggerGeneration
{

    public class SwaggerFromSPOptions
    {
        public string Path { get; set; } = "/swagger/v1/swagger.json";

        public bool PersistResultSets { get; set; } = true;
    }

}
