# Arquitetura de Interface e Experiência Omnichannel

## ProformaFarmERP — Diretrizes de UX/UI + Segurança Visual + Estrutura de Rotas
Versão: 1.0  
Status: Normativo (obrigatório para novas telas e fluxos)  
Codificação: UTF-8

---

## 1. Objetivo

Documentar a arquitetura de interface do ProformaFarmERP para duas frentes distintas e complementares:

1) **Backend (Painel de Controle / Retaguarda)**  
   Foco em gestão administrativa, financeira e regulatória (inclui SNGPC). Prioriza densidade de dados, produtividade, tabelas de alta performance, filtros avançados e dashboards.

2) **Frontend (Operação Omnichannel / PDV)**  
   Foco em velocidade de atendimento, fluxo simplificado, teclado/touch-friendly quando necessário, e operação segura em balcão.

Este documento também define requisitos de **segurança no design**, **isolamento de UI**, **sanitização** e **trilha de auditoria visual**, preparando o projeto para implementação assistida pelo Codex.

---

## 2. Premissas do Produto (não-negociáveis)

- O ProformaFarmERP **não** é API-only. O escopo oficial contempla **três camadas integradas**:
  - API de domínio e integração.
  - Painel backend (retaguarda administrativa).
  - Frontend de operação omnichannel (PDV e operação).
- A camada de interface é extensão da governança: **segurança, auditoria e contexto organizacional** devem ser visíveis e consistentes.
- O **OrgContext** é pilar estrutural: operações e visualização sempre alinhadas a **OrganizacaoId** e **UnidadeId**.

---

## 3. Stack Recomendada (UI Architecture)

### 3.1 Framework
- **Next.js (App Router)**
  - SSR para reforçar isolamento e impedir vazamento cross-tenant.
  - Middleware server-side para validação de sessão e escopo.
  - Boa integração com APIs .NET e execução de middlewares de segurança.

### 3.2 Design System e Componentes
Recomendação principal:
- **Tailwind CSS + shadcn/ui (Radix UI)**  
  Motivos:
  - Temas (Light/Dark) simples e consistentes.
  - Acessibilidade (ARIA) por padrão via Radix.
  - Controle visual total sem acoplamento forte.

Alternativa corporativa (se necessário):
- **Ant Design**
  - Bom para backoffice clássico e rápido, porém menos flexível para identidade visual moderna.

### 3.3 Padrões de Acessibilidade
Obrigatório:
- Contraste adequado (WCAG 2.1 AA como referência).
- Navegação por teclado no Painel.
- Estados de foco visíveis e consistentes.
- Componentes com ARIA quando aplicável.

---

## 4. Segurança no Design (Obrigatório)

### 4.1 Contexto Organizacional Sempre Visível
Para evitar erros operacionais (ex.: operar em filial errada), toda tela deve exibir **claramente**:

- Nome da Organização
- Nome/Identificador da Unidade
- Ambiente (Produção/Homolog)
- Usuário e perfil/permissão
- (Opcional) Cor/Badge por ambiente e por unidade

Requisito:
- O “Context Bar” deve ser **fixo no topo** e **não pode ser removido** em fluxos transacionais.

### 4.2 Isolamento de UI (Anti Cross-Tenant)
- O Frontend **nunca** deve receber dados fora do OrganizacaoId do contexto autenticado.
- Validação deve ocorrer em **SSR/middleware** e também na **API** (dupla validação).
- Proibido confiar em filtros client-side como mecanismo de isolamento.

### 4.3 Sanitização e Proteção contra XSS
Campos críticos:
- Busca de medicamentos/produtos
- Observações e descrições
- Campos livres em cadastros e atendimento

Obrigatório:
- Sanitização e escaping server-side.
- Renderização de HTML somente quando confiável (ex.: markdown convertido sob pipeline controlado).
- Proibir conteúdo ativo (scripts, iframes) em campos de texto.

### 4.4 Trilha de Auditoria Visual (Ações Críticas)
Ações críticas (exemplos):
- Alterar preço/tabela
- Excluir lote
- Ajustar estoque
- Cancelar documento fiscal
- Estornar recebimento

Obrigatório:
- Modal de confirmação com resumo do impacto.
- Exibir **CorrelationId** gerado pelo backend (ou previamente gerado pelo frontend e confirmado pelo backend).
- Mensagem explícita: “Esta ação será auditada.”
- Registrar auditoria no backend com CorrelationId, UsuarioId, OrganizacaoId, UnidadeId.

---

## 5. Layout — Backend (Painel de Controle / Retaguarda)

### 5.1 Objetivo de UX do Painel
- Alta densidade de dados e produtividade.
- Navegação não linear com múltiplas abas internas.
- Consultas avançadas com filtros por coluna, ordenação e views persistidas.

### 5.2 Estrutura de Layout (Padrão)
- **Top Bar fixo** (Context Bar):
  - Organização | Unidade (selector) | Ambiente | Usuário/perfil | Atalhos (opcional)
- **Sidebar esquerda fixa** com menus colapsáveis por domínio.
- **Área principal** com:
  - Tabs internas (estilo navegador) para trabalho paralelo.
  - Painéis e páginas com DataGrid e detalhes.

### 5.3 Sidebar (domínios sugeridos)
- Dashboard
- Comercial
  - Pedidos
  - Vendas
- Estoque
  - Produtos
  - Lotes
  - Movimentações
  - Inventário
- Fiscal
  - NF-e
  - NFC-e
  - Manifestos/Status
- Financeiro
  - Contas a Receber
  - Conciliação
  - Contratos (futuro)
- SNGPC
- Relatórios
- Integrações
- Administração

### 5.4 DataGrid (requisito enterprise)
O DataGrid do Painel deve suportar:
- Virtualização (linhas/colunas) para alta performance.
- Filtros por coluna e filtros avançados combináveis.
- Ordenação multi-coluna.
- Persistência de “views” (colunas + filtros + ordenação).
- Exportação controlada (CSV/PDF) com auditoria.

Implementação recomendada:
- TanStack Table + virtualização (controle máximo)  
ou
- AG Grid (se precisar do pacote enterprise completo)

---

## 6. Layout — Frontend (Operação Omnichannel / PDV)

### 6.1 Objetivo de UX do PDV
- Velocidade de atendimento e baixa fricção.
- Fluxo linear e previsível.
- Suporte a teclado e, se aplicável, touch (tablets no balcão).

### 6.2 Estrutura de Layout (Padrão)
- **Top Bar fixo** (Context Bar reduzida):
  - Organização | Unidade | Caixa/Turno (se aplicável) | Usuário
- **Canvas central** focado em:
  - Busca instantânea
  - Carrinho/Atendimento
  - Pagamento/Finalização
- **Painel lateral** (direita) para:
  - Perfil do paciente
  - Alertas (interações, pendências, restrições)
  - Serviços clínicos pendentes

### 6.3 Busca Instantânea (requisito)
- Debounce (ex.: 150–250ms) com limites por backend.
- Suporte a código de barras.
- Dropdown com resultado rápido (teclado-friendly).
- Proteções:
  - limitar tamanho do termo
  - rate limit por sessão
  - sanitização

### 6.4 Atalhos de teclado (sugestão de baseline)
- F1: Buscar produto
- F2: Finalizar venda
- F3: Pagamento
- F4: Cancelar item
- Esc: Fechar modal/retornar foco

### 6.5 Touch-friendly (quando aplicável)
- Botões e alvos clicáveis ampliados.
- Evitar menus pequenos e dropdowns densos no PDV.
- Preferir listas e botões grandes em tablet.

---

## 7. Responsividade e Breakpoints

Breakpoints recomendados:
- **>= 1536px**: Desktop amplo (retaguarda com múltiplas colunas)
- **>= 1280px**: Desktop padrão (retaguarda)
- **>= 1024px**: Laptop / tablet landscape (retaguarda simplificada)
- **>= 768px**: Tablet balcão (PDV touch-friendly)
- **>= 480px**: Mobile (consulta rápida, não recomendado para operação completa)

Regras:
- Painel: otimizado para desktop; tablet apenas para consulta.
- PDV: deve funcionar em tablet (>=768) com foco em velocidade.

---

## 8. Hierarquia de Rotas Proposta

### 8.1 Backend (Retaguarda)
- /dashboard
- /comercial/pedidos
- /comercial/vendas
- /estoque/produtos
- /estoque/lotes
- /estoque/movimentacoes
- /fiscal/nfe
- /fiscal/nfce
- /financeiro/contas-a-receber
- /financeiro/conciliacao
- /sngpc
- /relatorios
- /integracoes
- /administracao

### 8.2 Frontend (Operação/PDV)
- /pdv
- /pdv/atendimento
- /pdv/pagamento
- /pdv/cliente (perfil do paciente, quando necessário)
- /clinica/atendimento (fase futura, dependente de governança de dados sensíveis)

---

## 9. Componentes Principais por Camada

### 9.1 Cross-cutting (ambos)
- ContextBar (Organizacao/Unidade/Ambiente/Usuário)
- AuditConfirmModal (ação crítica + CorrelationId)
- Toast/Notifications (com padronização)
- ErrorBoundary + EmptyState (padronizados)

### 9.2 Retaguarda
- SidebarMenu (domínios colapsáveis)
- TabsWorkspace (abas internas)
- DataGridEnterprise
- AdvancedFiltersPanel
- DashboardCards + KPIWidgets

### 9.3 PDV/Operação
- FastSearchBox (barcode + autocomplete)
- CartCanvas (itens, quantidades, descontos com regras)
- PatientProfilePanel (alertas e histórico rápido)
- PaymentFlowWizard (etapas claras, teclado-friendly)

---

## 10. Integração com Auditoria e CorrelationId

### 10.1 Geração e propagação
- O backend deve emitir CorrelationId por request (middleware) e retornar em header e/ou payload padrão.
- A UI deve exibir CorrelationId em:
  - modal de ações críticas
  - tela de erro (com copy-to-clipboard)

### 10.2 Requisitos de log
- Toda ação crítica deve registrar:
  - CorrelationId
  - UsuarioId
  - OrganizacaoId
  - UnidadeId
  - entidade/ação
  - antes/depois (quando aplicável)

---

## 11. Diretrizes para Implementação (Codex)

### 11.1 Regras de trabalho
- Implementar primeiro a infraestrutura (ContextBar, ThemeProvider, Auth guard SSR, layout base).
- Depois criar rotas e páginas “skeleton” com componentes padronizados.
- Só então implementar grids e fluxos transacionais.

### 11.2 Prompt para o Codex (copiar/colar)
Use o texto abaixo como instrução ao Codex no VS Code.

---

#### PROMPT CODEX — UI Architecture (Retaguarda + PDV)

Contexto:
Você está no repositório ProformaFarmERP. O produto não é API-only: existe Painel (Retaguarda) e Frontend de Operação (PDV).
OrgContext é obrigatório e o contexto OrganizacaoId/UnidadeId deve ser visível em todas as telas.

Fonte obrigatória:
Leia e aplique o documento: docs/architecture/Arquitetura de Interface e Experiência Omnichannel.md

Objetivo desta rodada (escopo fechado):
Criar a estrutura inicial do frontend (Next.js App Router) com:
1) ThemeProvider (Light/Dark) e tokens base.
2) ContextBar fixa exibindo Organizacao/Unidade/Ambiente/Usuário.
3) Layout do Painel (sidebar + tabs workspace placeholder) e layout do PDV (canvas + painel lateral).
4) Rotas skeleton (sem lógica transacional), com componentes padrão.
5) Guard SSR (middleware) para impedir acesso sem sessão e para validar escopo.
6) Componentes base: AuditConfirmModal (placeholder), ErrorBoundary, EmptyState, Toast.

Regras:
- Nenhuma tela pode existir sem ContextBar.
- Proibir renderização de HTML não confiável.
- Preparar pontos de integração para CorrelationId (exibir em erros e no modal de ações críticas).
- Responsividade conforme breakpoints definidos no documento.

Plano:
- Antes de codar, listar pastas/arquivos que serão criados.
- Implementar em commits pequenos (layout, componentes base, rotas skeleton).
- Ao final, rodar build e testes/unit quando existirem.

Definition of Done:
- App Next.js sobe com layout de Painel e PDV.
- Theme light/dark funcional.
- ContextBar sempre visível.
- Rotas skeleton implementadas.
- Middleware SSR/guard aplicado.
- Documentação breve adicionada no README da UI.

---

## 12. Governança de Mudanças

Qualquer mudança estrutural (rotas, layouts, breakpoints, componentes base) deve:
- atualizar este documento
- incluir checklist de segurança visual (ContextBar + auditoria)
- manter consistência entre Painel e PDV

---

## 13. Anexos (Checklist de Segurança Visual)

Checklist obrigatório para revisão de PR:
- [ ] ContextBar exibe Organizacao e Unidade em todas as telas
- [ ] Ações críticas usam modal com confirmação e CorrelationId
- [ ] Inputs de busca sanitizados e com limites
- [ ] SSR/guard impede acesso sem sessão
- [ ] Nenhum dado cross-tenant é renderizado
- [ ] Logs e erros exibem CorrelationId
