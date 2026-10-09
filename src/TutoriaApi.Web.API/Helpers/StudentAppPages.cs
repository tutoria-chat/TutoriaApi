using System.Net;
using TutoriaApi.Core.Constants;

namespace TutoriaApi.Web.API.Helpers;

/// <summary>
/// Server-rendered pages for TutorIA Estudantes: the guardian consent page (opened
/// from the consent email, so the guardian needs nothing installed) and the legal
/// pages the app and the stores link to (privacy, terms, account deletion).
/// </summary>
public static class StudentAppPages
{
    private const string Style = @"
body{font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif;background:#F5F4FB;color:#16123A;margin:0}
main{max-width:560px;margin:0 auto;padding:32px 20px 48px}
h1{color:#5E17EB;font-size:26px;margin:0 0 4px} h2{font-size:19px;margin:24px 0 8px}
.card{background:#fff;border-radius:16px;padding:20px;box-shadow:0 1px 3px rgba(0,0,0,.06);margin-top:16px}
ul,ol{padding-left:20px;line-height:1.55} p{line-height:1.55}
.row{display:flex;gap:12px;margin-top:20px;flex-wrap:wrap}
button{flex:1;min-width:180px;font-size:16px;padding:14px;border-radius:12px;border:0;cursor:pointer;font-weight:600}
.ok{background:#5E17EB;color:#fff} .no{background:#fff;color:#B42318;border:1.5px solid #B42318}
small{color:#666}";

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    public static string Page(string title, string body)
        => "<!doctype html><html lang='pt-BR'><head><meta charset='utf-8'>" +
           "<meta name='viewport' content='width=device-width,initial-scale=1'>" +
           $"<title>{E(title)} · TutorIA Estudantes</title><style>{Style}</style></head>" +
           $"<body><main><h1>TutorIA Estudantes</h1>{body}</main></body></html>";

    public static string InvalidLink()
        => Page("Link inválido",
            "<div class='card'><h2>Link inválido ou expirado</h2>" +
            "<p>Peça para o estudante enviar um novo pedido de autorização pelo app.</p></div>");

    public static string GuardianConsent(string token, string studentName, string? guardianName, string dpoEmail)
    {
        var hello = string.IsNullOrWhiteSpace(guardianName) ? "Olá!" : $"Olá, {E(guardianName)}!";
        return Page("Autorização do responsável", $@"
<p>{hello} <b>{E(studentName)}</b> criou uma conta no TutorIA Estudantes e indicou você como responsável.</p>
<div class='card'>
<h2>O que é o TutorIA Estudantes</h2>
<p>Um app de estudos com inteligência artificial, do Tutoria. O estudante conversa com agentes de estudo, resolve
questões oficiais do ENEM, revisa com flashcards e, em alguns planos, recebe correções de redação feitas por IA (com
nota sugerida, que pode não ser 100% precisa) ou estuda com o material da faculdade.</p>
<h2>Dados que tratamos</h2>
<ul>
<li>Nome, e-mail e data de nascimento do estudante; seu nome e e-mail como responsável.</li>
<li>Mensagens trocadas com os agentes, respostas às questões, flashcards, redações e materiais de estudo enviados.</li>
<li>Fotos enviadas são processadas pela IA e não são guardadas.</li>
</ul>
<p>O conteúdo é processado por provedores de inteligência artificial para gerar as respostas. Não vendemos dados, não
exibimos anúncios e não usamos os dados para publicidade. As compras de assinatura passam pela App Store ou pelo
Google Play, com os controles de aprovação familiar da loja. O estudante pode exportar ou apagar a conta no app.</p>
<p><small>Dúvidas ou para revogar esta autorização: {E(dpoEmail)}</small></p>
</div>
<form method='post' action='/api/student-app/guardian/consent'>
<input type='hidden' name='token' value='{E(token)}'>
<div class='row'>
<button class='ok' name='decision' value='approve'>Autorizo o uso do app</button>
<button class='no' name='decision' value='deny'>Não autorizo</button>
</div></form>");
    }

    public static string GuardianDecided(string studentName, bool approved)
        => Page("Decisão registrada", approved
            ? $"<div class='card'><h2>Obrigado!</h2><p>A conta de <b>{E(studentName)}</b> foi liberada. Já dá para voltar ao app e começar a estudar.</p></div>"
            : "<div class='card'><h2>Tudo certo</h2><p>Registramos que você não autorizou. A conta continua bloqueada para uso.</p></div>");

    private static string Company(StudentAppOptions o) => $"{E(o.CompanyName)}, CNPJ {E(o.CompanyCnpj)}";

    public static string Privacy(StudentAppOptions o) => Page("Política de Privacidade", $@"
<h2>Política de Privacidade</h2>
<p>O TutorIA Estudantes é um produto do Tutoria, operado por {Company(o)} (""nós""). Esta política explica quais dados
pessoais tratamos e por quê, nos termos da LGPD (Lei 13.709/2018) e do Estatuto Digital da Criança e do Adolescente
(Lei 15.211/2025).</p>
<h2>1. Dados que coletamos</h2>
<ul>
<li><b>Conta:</b> nome, e-mail, senha (guardada de forma criptografada) e data de nascimento.</li>
<li><b>Responsável (menores de 18):</b> nome e e-mail do responsável e o registro da autorização.</li>
<li><b>Estudo:</b> mensagens trocadas com os agentes, respostas a questões, flashcards, redações e suas correções,
pontos e sequência de estudos.</li>
<li><b>Material da faculdade (plano Universitário):</b> os arquivos que você envia (PDFs, slides, documentos, fotos),
organizados por disciplina, e o texto extraído deles, visíveis apenas para você; também instituição, curso, semestre e
disciplinas que você informar.</li>
<li><b>Fotos no chat e de redações:</b> são enviadas à IA para leitura e não são armazenadas.</li>
<li><b>Sinal de idade da loja:</b> se você permitir, a faixa etária informada pela Apple ou pelo Google, usada apenas
para aplicar as proteções para menores.</li>
<li><b>Notificações:</b> o identificador de notificações do seu aparelho. Você pode desligar no app.</li>
<li><b>Assinatura:</b> o pagamento é feito pela App Store ou pelo Google Play; não recebemos dados de cartão.</li>
</ul>
<h2>2. Preferências de aprendizagem (opcional)</h2>
<p>Em ""Seu jeito de aprender"" você pode indicar características que influenciam seu aprendizado. Essas escolhas
<b>ficam apenas no seu aparelho</b>: enviamos somente preferências de estilo de resposta, nunca a característica em si.</p>
<h2>3. Para que usamos</h2>
<ul>
<li>Prestar o serviço contratado: gerar respostas, correções e materiais de estudo (execução de contrato).</li>
<li>Verificar a idade e obter a autorização do responsável quando necessário (obrigação legal).</li>
<li>Segurança, prevenção de abuso e limites de uso (legítimo interesse).</li>
</ul>
<p>Não vendemos dados, não exibimos publicidade e não traçamos perfil comercial de crianças e adolescentes.</p>
<h2>4. Compartilhamento e transferência internacional</h2>
<p>O conteúdo é processado por provedores de inteligência artificial e de infraestrutura em nuvem (como a AWS), que
podem estar fora do Brasil, apenas para operar o serviço e sob contrato.</p>
<h2>5. Por quanto tempo guardamos</h2>
<p>Enquanto sua conta existir; conversas ficam disponíveis por até 90 dias. Ao excluir a conta, apagamos seus dados em
definitivo, exceto o que a lei nos obrigar a manter.</p>
<h2>6. Seus direitos</h2>
<p>No app (Perfil → Privacidade) você pode baixar uma cópia dos seus dados e excluir sua conta. Para outros pedidos
escreva para o encarregado de dados: {E(o.DpoEmail)}.</p>
<h2>7. Inteligência artificial</h2>
<p>As respostas e correções são geradas por IA e podem conter erros. Notas de redação são <b>sugestões</b> e não
substituem a avaliação de um professor.</p>");

    public static string Terms(StudentAppOptions o) => Page("Termos de Uso", $@"
<h2>Termos de Uso</h2>
<p>Ao criar uma conta no TutorIA Estudantes, operado por {Company(o)}, você concorda com estes termos.</p>
<h2>1. O serviço</h2>
<p>O TutorIA Estudantes é um app de estudos com inteligência artificial. Ele não é uma instituição de ensino, não
emite certificados e não garante aprovação em exames.</p>
<h2>2. Conta e idade</h2>
<p>É preciso ter pelo menos 13 anos. Menores de 18 anos só podem usar o app com a autorização de um responsável legal.</p>
<h2>3. Planos e assinatura</h2>
<p>As assinaturas são cobradas e renovadas pela App Store ou pelo Google Play, e o cancelamento é feito na loja.
As correções de redação do plano Max renovam todo domingo à meia-noite (horário de Brasília) e não são cumulativas.</p>
<h2>4. Uso aceitável</h2>
<p>Não use o app para fins ilegais, para assediar outras pessoas ou para burlar os limites e a segurança do serviço.</p>
<h2>5. Material enviado</h2>
<p>Ao enviar arquivos no plano Universitário, você declara ter o direito de usá-los para seu estudo pessoal (por
exemplo, material disponibilizado pela sua instituição). O material é usado só para responder às suas perguntas. Não
envie dados pessoais de terceiros.</p>
<h2>6. Conteúdo gerado por IA</h2>
<p>As respostas, explicações e notas sugeridas são produzidas por IA, podem conter erros e devem ser usadas como apoio
ao estudo. Recomendamos conferir informações importantes e buscar a revisão de um professor.</p>
<h2>7. Contato</h2>
<p>{E(o.DpoEmail)}</p>");

    public static string DeleteAccount(StudentAppOptions o) => Page("Excluir sua conta", $@"
<h2>Excluir sua conta</h2>
<p>Você pode excluir sua conta e todos os seus dados a qualquer momento:</p>
<ol><li>Abra o app TutorIA Estudantes e entre na sua conta.</li>
<li>Vá em <b>Perfil → Privacidade → Excluir minha conta</b>.</li><li>Confirme com sua senha.</li></ol>
<p>A exclusão é imediata e definitiva. Lembre-se de cancelar a assinatura na App Store ou no Google Play.</p>
<p>Sem acesso ao app? Escreva para {E(o.DpoEmail)} usando o e-mail da conta.</p>");
}
