// Per-repo sops config for tastile-desktop.
//
// The canonical SopsEnvConfig type lives at tastile-root/scripts/sops.config.ts
// (committed at 019d248) and lacks the `identityHint` field introduced by Task 3.
// Since SopsEnvConfig is a `type` alias (not an `interface`), TypeScript module
// augmentation cannot extend it. Per-repo config files therefore re-define the
// full type with `identityHint` appended. A loader copy is NOT required in
// tastile-desktop for Task 3 (the loader lives in tastile-web as the test
// consumer); when tastile-desktop later ships its own runner, it will reuse
// this shape via its own `import { config } from "./sops.config"`.
//
// Replace <account> placeholders with Terraform outputs from Task 1
// (terraform apply in tastile-root/infra/).

export type SopsEnvConfig = {
  kmsKeyArn: string;
  awsRegion: string;
  sourceFiles: string[];
  targetFiles: string[];
  check: boolean;
  identityHint: "sso" | "oidc" | "instance-profile";
};

export const config: Record<string, SopsEnvConfig> = {
  development: {
    awsRegion: "ap-northeast-1",
    kmsKeyArn: "arn:aws:kms:ap-northeast-1:<account>:key/<dev-key-id>",
    sourceFiles: [".env.development.sops", ".env.dev.sops"],
    targetFiles: [".env.development", ".env.dev"],
    check: false,
    identityHint: "sso",
  },
  staging: {
    awsRegion: "ap-northeast-1",
    kmsKeyArn: "arn:aws:kms:ap-northeast-1:<account>:key/<staging-key-id>",
    sourceFiles: [".env.staging.sops"],
    targetFiles: [".env.staging"],
    check: false,
    identityHint: "oidc",
  },
  production: {
    awsRegion: "ap-northeast-1",
    kmsKeyArn: "arn:aws:kms:ap-northeast-1:<account>:key/<production-key-id>",
    sourceFiles: [".env.production.sops", ".env.product.sops"],
    targetFiles: [".env.production", ".env.product"],
    check: false,
    identityHint: "instance-profile",
  },
};