# MAPA DE EVOLUÇÃO MODULAR: PROFORMAFARM ERP

## 1. Visão de Produto (Filtro Farmacêutico)
O ProformaFarm foca na excelência operacional e conformidade. Foram removidos módulos de manufatura/externos para priorizar a robustez no varejo e distribuição de saúde.

## 2. Novos Domínios de Controladoria (Campos Obrigatórios)

### A. Módulo: Conciliação de Cartões
**Objetivo:** Automação de recebíveis e prevenção de perdas financeiras.
- **Tabela:** `Financeiro.ConciliacaoCartao`
- **Campos:**
  - `Id`, `OrganizacaoId`, `UnidadeId` (Padrão OrgContext).
  - `NsuTransacao` (string): Identificador da operadora.
  - `CodigoAutorizacao` (string) : **[SensitiveData]** - Protegido pelo Guardian.
  - `ValorBruto`, `TaxaAdm`, `ValorLiquido` (decimal).
  - `DataPrevisaoPagamento` (DateTime).
  - `StatusConciliacao` (Enum: Pendente, Sucesso, Divergente).

### B. Módulo: Escrita Fiscal
**Objetivo:** Compliance com impostos indiretos (ICMS-ST) e obrigações estaduais/federais.
- **Tabela:** `Fiscal.EscritaFiscal`
- **Campos:**
  - `ChaveAcessoNFe` (string, 44 chars): Indexado para busca.
  - `CpfCnpjDestinatario` (string): **[SensitiveData]** - Proteção LGPD para pacientes.
  - `XmlConteudo` (varbinary/blob): Criptografado em repouso.
  - `ValorTotalBaseICMS`, `ValorTotalST` (decimal).
  - `StatusSefaz` (Enum).

## 3. Integração Guardian (Segurança Transparente)
Todo o acesso a estes campos sensíveis será mediado pelo `EncryptConverter` no EF Core, garantindo que o banco de dados seja seguro mesmo em caso de dump não autorizado.
