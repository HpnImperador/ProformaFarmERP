# Atualização de Diretrizes Arquiteturais Obrigatórias v2

Adicionar ao item "Regras Obrigatórias":

- Blindagem de Dados: Campos sensíveis de saúde devem obrigatoriamente usar o atributo `[SensitiveData]`.
- Trilha de Auditoria: Nenhuma exclusão física é permitida em tabelas do esquema `Clinica` ou `Financeiro` (uso obrigatório de Soft Delete + Log de Auditoria).
- Regra 11: É proibida a persistência de dados sensíveis de pacientes (CPF, Prontuário, Chaves de Autorização) em texto plano. O uso do atributo `[SensitiveData]` é obrigatório nestes domínios.
