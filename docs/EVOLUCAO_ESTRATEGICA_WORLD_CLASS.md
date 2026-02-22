
1. Visão Geral
Este documento detalha a modelagem de dados para os novos módulos de alta performance do ProformaFarm ERP. O objetivo é garantir que a estrutura suporte inteligência preditiva e serviços de saúde, mantendo o isolamento por OrgContext.

2. Esquema de Banco de Dados (Novos Módulos)
A. Módulo de Farmácia Clínica (Clinical Services)
Este módulo permite que a farmácia deixe de ser apenas um ponto de venda e se torne um hub de saúde.

SQL
-- Tabela de Serviços Clínicos (Vacinas, Testes, Aferições)
CREATE TABLE Clinica.ServicosPrestados (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrganizacaoId UNIQUEIDENTIFIER NOT NULL, -- Obrigatório (OrgContext)
    UnidadeId UNIQUEIDENTIFIER NOT NULL,      -- Onde o serviço foi feito
    PacienteId UNIQUEIDENTIFIER NOT NULL,     -- Vinculado ao cadastro de clientes
    FarmaceuticoId UNIQUEIDENTIFIER NOT NULL, -- Usuário (Cargo: Farmacêutico)
    TipoServico INT NOT NULL,                 -- Enum (1: Glicemia, 2: Pressão, 3: Vacina)
    DataAtendimento DATETIMEOFFSET NOT NULL,
    ObservacoesClinicas NVARCHAR(MAX),
    DadosResultados JSON NOT NULL,            -- Armazena os valores (ex: { "sis": 120, "dia": 80 })
    ProximaRevisao DATETIMEOFFSET NULL,       -- Agendamento para retorno
    CreatedAt DATETIMEOFFSET NOT NULL,
    
    CONSTRAINT FK_Servicos_Organizacao FOREIGN KEY (OrganizacaoId) REFERENCES Core.Organizacoes(Id)
);

CREATE INDEX IX_Servicos_Paciente ON Clinica.ServicosPrestados (PacienteId, DataAtendimento);
B. Módulo de CRM e Retenção (Adesão do Paciente)
Focado em garantir que o paciente de uso contínuo não esqueça de comprar o medicamento.

SQL
-- Tabela de Monitoramento de Adesão
CREATE TABLE CRM.AdesaoTratamento (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrganizacaoId UNIQUEIDENTIFIER NOT NULL,
    PacienteId UNIQUEIDENTIFIER NOT NULL,
    ProdutoId UNIQUEIDENTIFIER NOT NULL,      -- Medicamento de uso contínuo
    UltimaCompraData DATETIMEOFFSET NOT NULL,
    PrevisaoTermoData DATETIMEOFFSET NOT NULL, -- Calculado com base na posologia
    StatusNotificacao INT DEFAULT 0,          -- Enum (Pendente, Enviado, Convertido)
    CorrelationId UNIQUEIDENTIFIER NULL,      -- Vinculado à venda que originou o registro
    
    CONSTRAINT FK_Adesao_Organizacao FOREIGN KEY (OrganizacaoId) REFERENCES Core.Organizacoes(Id)
);
C. Módulo de Inteligência de Estoque (Demand Forecasting)
Estrutura para alimentar algoritmos de IA que evitam a falta de produtos.

SQL
-- Tabela de Sugestão de Compra Preditiva (Alimentada por IA/Background Job)
CREATE TABLE Estoque.SugestoesCompraIA (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrganizacaoId UNIQUEIDENTIFIER NOT NULL,
    UnidadeId UNIQUEIDENTIFIER NOT NULL,
    ProdutoId UNIQUEIDENTIFIER NOT NULL,
    QuantidadeSugerida DECIMAL(18,4) NOT NULL,
    ConfiancaPredicao DECIMAL(5,2) NOT NULL,  -- Porcentagem (ex: 0.95 para 95%)
    MotivoPredicao NVARCHAR(255),             -- Ex: "Sazonalidade Inverno", "Ruptura Iminente"
    DataExpiracaoSugestao DATETIMEOFFSET NOT NULL,
    
    CONSTRAINT FK_Sugestoes_Unidade FOREIGN KEY (UnidadeId) REFERENCES Core.Unidades(Id)
);
3. Integração com Domain Events
Para que esses módulos funcionem de forma desacoplada, o sistema deve disparar eventos:

VendaFinalizadaEvent: O módulo de CRM escuta este evento. Se o produto for marcado como "Uso Contínuo", ele cria/atualiza um registro em CRM.AdesaoTratamento.

ServicoClinicoRegistradoEvent: Dispara um alerta para o histórico do paciente no Frontend de Operação Omnichannel.

EstoqueBaixoEvent: Notifica o módulo de IA para reavaliar a urgência de uma nova sugestão de compra.

4. Diretrizes de Implementação para o Time de Dev / IA
Segurança: Nunca execute Selects em Clinica ou CRM sem o filtro de OrganizacaoId.

Performance: Utilize a camada de leitura (Dapper) para os Dashboards de gestão de pacientes, garantindo resposta rápida mesmo com milhões de registros.

Auditoria: Como são dados sensíveis (saúde), toda inserção em Clinica.ServicosPrestados deve gerar uma trilha de auditoria detalhada.