namespace Conversor_de_Arquivos.Geo
{
    // Definições WKT de sistemas projetados usados para cálculo de área no Brasil.
    // O datum geográfico de origem é SIRGAS 2000; dados em WGS 84 são tratados como equivalentes
    // (diferença submétrica, irrelevante para área).
    public static class GeoProjections
    {
        // EPSG:5880 — SIRGAS 2000 / Brazil Polyconic.
        // Atenção: a Policônica NÃO é de área igual; a distorção de escala cresce com a distância
        // ao meridiano central (-54°). Para razões de área dentro de uma mesma região pequena
        // (ex.: % de um município) o efeito se cancela quase totalmente.
        public const string BrazilPolyconic5880 =
            "PROJCS[\"SIRGAS 2000 / Brazil Polyconic\"," +
            "GEOGCS[\"SIRGAS 2000\",DATUM[\"Sistema_de_Referencia_Geocentrico_para_las_AmericaS_2000\"," +
            "SPHEROID[\"GRS 1980\",6378137,298.257222101]],PRIMEM[\"Greenwich\",0]," +
            "UNIT[\"degree\",0.0174532925199433]]," +
            "PROJECTION[\"Polyconic\"]," +
            "PARAMETER[\"latitude_of_origin\",0],PARAMETER[\"central_meridian\",-54]," +
            "PARAMETER[\"false_easting\",5000000],PARAMETER[\"false_northing\",10000000]," +
            "UNIT[\"metre\",1],AUTHORITY[\"EPSG\",\"5880\"]]";

        // SIRGAS 2000 / Brazil Albers (parâmetros do IBGE) — projeção de área igual.
        public const string BrazilAlbersEqualArea =
            "PROJCS[\"SIRGAS 2000 / Brazil Albers\"," +
            "GEOGCS[\"SIRGAS 2000\",DATUM[\"Sistema_de_Referencia_Geocentrico_para_las_AmericaS_2000\"," +
            "SPHEROID[\"GRS 1980\",6378137,298.257222101]],PRIMEM[\"Greenwich\",0]," +
            "UNIT[\"degree\",0.0174532925199433]]," +
            "PROJECTION[\"Albers_Conic_Equal_Area\"]," +
            "PARAMETER[\"latitude_of_center\",-12],PARAMETER[\"longitude_of_center\",-54]," +
            "PARAMETER[\"standard_parallel_1\",-2],PARAMETER[\"standard_parallel_2\",-22]," +
            "PARAMETER[\"false_easting\",5000000],PARAMETER[\"false_northing\",10000000]," +
            "UNIT[\"metre\",1]]";
    }
}
