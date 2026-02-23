# GUARDIAN DEEP DIVE: CRIPTOGRAFIA E RESILIÊNCIA

## 1. Algoritmo de Criptografia
Utilizamos **AES-256-GCM** para garantir confidencialidade e integridade.
- **Chave de Proteção (KEK):** Armazenada no Key Vault (AWS/Azure).
- **Chave de Dados (DEK):** Derivada por Organização, garantindo que o tenant A não decifre dados do tenant B.

## 2. Estratégia de Backup & Restore
- **Backup:** Automatizado a cada 15 min (Transaction Logs).
- **Simulação de Restore:** Job mensal automático que valida a integridade do dump e reporta ao `Guardian.BackupHealth`.

## 3. Monitoramento Proativo
O sistema utiliza os `Domain Events` para detectar:
- **Exfiltração:** Mais de 500 registros exportados por usuário comum em 10 min.
- **Invasão:** IPs diferentes para o mesmo usuário em intervalo menor que 1h.
