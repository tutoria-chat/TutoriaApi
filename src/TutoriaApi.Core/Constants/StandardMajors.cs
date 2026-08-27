namespace TutoriaApi.Core.Constants;

/// <summary>
/// A default list of common Brazilian degree programs (graduações) that a
/// university starts from. Institutions can add their own on top, or remove
/// ones they don't offer. Used to seed a new university and to power the
/// "add standard majors" action for existing ones.
/// </summary>
public static class StandardMajors
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Administração",
        "Agronomia",
        "Arquitetura e Urbanismo",
        "Biomedicina",
        "Ciência da Computação",
        "Ciências Biológicas",
        "Ciências Contábeis",
        "Ciências Econômicas",
        "Direito",
        "Educação Física",
        "Enfermagem",
        "Engenharia Civil",
        "Engenharia de Produção",
        "Engenharia de Software",
        "Engenharia Elétrica",
        "Engenharia Mecânica",
        "Engenharia Química",
        "Farmácia",
        "Fisioterapia",
        "Fonoaudiologia",
        "Gestão de Recursos Humanos",
        "História",
        "Jornalismo",
        "Letras",
        "Marketing",
        "Matemática",
        "Medicina",
        "Medicina Veterinária",
        "Nutrição",
        "Odontologia",
        "Pedagogia",
        "Psicologia",
        "Publicidade e Propaganda",
        "Química",
        "Sistemas de Informação",
        "Serviço Social",
    };
}
