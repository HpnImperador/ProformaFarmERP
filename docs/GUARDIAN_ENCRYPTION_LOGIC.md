# GUARDIAN: LÓGICA DE CRIPTOGRAFIA DE CAMPOS SENSÍVEIS

## 1. Implementação do Value Converter (EF Core)
A IA deve implementar o conversor no `DbContext` para que a criptografia ocorra no pipeline de persistência.

```csharp
// Infrastructure/Security/EncryptConverter.cs
public class EncryptConverter : ValueConverter<string, string>
{
    public EncryptConverter(ICryptoProvider cryptoProvider, IOrgContext orgContext)
        : base(
            v => cryptoProvider.Encrypt(v, orgContext.OrganizacaoId), // Na escrita
            v => cryptoProvider.Decrypt(v, orgContext.OrganizacaoId)  // na leitura
        ) { }
}
```

---

## 2. Regras de Auditoria do Guardian

- **Ações Críticas:** Toda leitura de campo `[SensitiveData]` fora do PDV gera um log no `Guardian.AuditLog`.
- **Prevenção de Fraude:** A IA monitora divergências no módulo de Conciliação e bloqueia automaticamente estornos manuais suspeitos que não possuem o `CorrelationId` da venda original.

---

## 3. Atualização das Diretrizes e Roadmap

### Em `DIRETRIZES_ARQUITETURAIS_OBRIGATORIAS_v2.md`
- **Nova Regra (11):** "É proibida a persistência de dados sensíveis de pacientes (CPF, Prontuário, Chaves de Autorização) em texto plano. O uso do atributo `[SensitiveData]` é obrigatório nestes domínios."

### Em `README.md` (Seção de Governança)
- Atualizado para incluir: **"Resiliência Proativa com Módulo Guardian (Monitoramento de Backups e Criptografia de Ponta-a-Ponta)"**.

---

## 4. Desenho do Dashboard de Riscos (Frontend)

Para a IA de interface, segue o esquema de componentes do **Dashboard Guardian**:

1. **Widget "Status de Saúde da Organização":** Score de 0 a 100 baseado em:
   - Backups realizados nas últimas 24h.
   - Pendências de Conciliação de Cartão.
   - Alertas de divergência fiscal.
2. **Widget "Radar de Vulnerabilidades":** Lista de logins realizados em horários atípicos ou a partir de novos dispositivos/IPs.
3. **Monitor de Criptografia:** Contador em tempo real de quantos registros estão protegidos pelo Guardian no banco de dados.

---

## Como prosseguir agora?

Os documentos acima elevam o **ProformaFarm** à categoria de "Sistema de Missão Crítica".

Sugestão para geração dos **Scripts SQL definitivos** para as tabelas de **Fiscal e Conciliação**, já incluindo os campos de auditoria e segurança do Guardian.
