using System.Reflection;

namespace HuGeo.Resources;

public static class EmbeddedResources
{
    public static Assembly Assembly => typeof(EmbeddedResources).Assembly;
}
