# Segurança, Resiliência e Governança de Dados

## 1. Análise da Situação Atual (Status Check)

Pilar de Contexto (OrgContext): Está consolidado e sendo forçado via Middleware. Isso é a base para o Módulo Guardian, pois a segurança será aplicada por camada de organização.

Mensageria e Eventos: A implementação do Outbox Pattern e do Event Relay (visto no resumo técnico item 50) é o "gancho" perfeito para o Guardian monitorar ameaças em tempo real sem degradar a performance do banco principal.

Interface (UI/UX): O documento de "Arquitetura de Interface Omnichannel" já prevê a ContextBar e o AuditConfirmModal, que são as faces visíveis do Guardian para o usuário final.

## 2. Estruturação do "Módulo Guardian"

Para atender ao seu pedido de proteção, criptografia, backup e análise proativa, o Módulo Guardian deve ser implementado como um novo domínio core.

### A. Camada de Persistência e Monitoramento (SQL)

Este esquema foca em identificar vulnerabilidades e garantir a resiliência:

```sql
-- Esquema dedicado para Segurança e Governança
CREATE SCHEMA Guardian;

-- 1. Monitoramento de Integridade e Resiliência (Backup)
CREATE TABLE Guardian.ResilienciaBackup (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrganizacaoId UNIQUEIDENTIFIER NOT NULL,
    StatusCheck INT,              -- (Saudável, Falha, Pendente)
    UltimoBackupValido DATETIMEOFFSET,
    LocalArmazenamento VARCHAR(255), -- Cloud Region / Bucket
    ChecksumValidacao VARCHAR(255),
    TempoRestauracaoEstimado INT, -- Em minutos (RTO)
    CONSTRAINT FK_Backup_Org FOREIGN KEY (OrganizacaoId) REFERENCES Core.Organizacoes(Id)
);

-- 2. Gestão de Chaves e Criptografia (KMS Interno)
CREATE TABLE Guardian.KeyVaultStore (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrganizacaoId UNIQUEIDENTIFIER NOT NULL,
    ChavePublicaReferencia VARCHAR(500),
    AlgoritmoUtilizado VARCHAR(50),      -- AES-256, RSA
    DataRotacaoChave DATETIMEOFFSET,
    StatusChave INT                      -- (Ativa, Revogada, Pendente)
);

-- 3. Análise de Vulnerabilidades e Ameaças (SIEM)
CREATE TABLE Guardian.ThreatIntelligence (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrganizacaoId UNIQUEIDENTIFIER NOT NULL,
    NivelRisco INT,                      -- (1: Baixo a 4: Crítico)
    OrigemAmeaça VARCHAR(100),           -- IP, UsuarioId ou Endpoint
    DescricaoAmeaça NVARCHAR(MAX),       -- Ex: "Múltiplas tentativas de exportação SNGPC"
    AcaoTomada INT,                      -- (Bloqueio Automático, Alerta Enviado, Ignorado)
    CorrelationId UNIQUEIDENTIFIER       -- Rastreabilidade total com o log de erro/venda
);
```

## 3. Funcionalidades de Defesa Proativa (Evolução)

Para que o Guardian seja "inteligente" como você solicitou, ele deve operar sobre os Domain Events que a IA já está implementando:

Monitoramento de Movimentação sem Dificuldade: O Guardian escutará o EventRelay. Sempre que houver um evento de "Exportação de Dados" ou "Acesso a prontuário clínico", o Guardian registra um log de auditoria imutável com CorrelationId.

Criptografia Dinâmica: No momento da persistência (EF Core), campos marcados como `[SensitiveData]` serão criptografados usando a chave da organização no `Guardian.KeyVaultStore`.

Identificação de Lacunas (Análise Avançada): O módulo terá um Job de background que compara o uso do sistema com padrões de segurança (ex: usuários sem MFA ativo ou unidades com backups falhando há mais de 24h).
