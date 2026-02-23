# ESPECIFICAÇÃO TÉCNICA: MÓDULO GUARDIAN (SEGURANÇA E GOVERNANÇA)

## 1. Escopo de Proteção
O Módulo Guardian é o guardião da integridade do ProformaFarm ERP, focado em LGPD, resiliência de dados e defesa proativa.

## 2. Pilares de Implementação
- **Criptografia Multi-tenant:** Chaves de criptografia únicas por `OrganizacaoId`.
- **Zero-Trust Logging:** Todo acesso a dados sensíveis (Clínica/Financeiro) gera um `CorrelationId` rastreável.
- **Resiliência Automatizada:** Monitoramento contínuo de Backups e Point-in-Time Recovery.

## 3. Integração com IA Preditiva
O Guardian utilizará análises avançadas para:
- Detectar anomalias de acesso (ex: login simultâneo em regiões distantes).
- Priorizar riscos de segurança baseados em volume de dados acessados.
- Notificar proativamente sobre falhas de compliance antes de auditorias oficiais (SNGPC).

## 4. Diretrizes para a IA de Desenvolvimento
- **Escrita:** Toda nova entidade de segurança deve seguir o padrão de persistência EF Core com auditoria.
- **Leitura:** Dashboards de segurança devem usar Dapper para não impactar a performance operacional.
- **Eventos:** O Guardian deve ser o principal "Subscriber" dos eventos de sistema para monitoramento passivo.

---
*Documento aprovado como extensão das Diretrizes Arquiteturais Obrigatórias v2.*
