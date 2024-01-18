{{/*
Create service name as used by the service name label.
*/}}
{{- define "service.fullname" -}}
{{- if not .Values.isExporter }}
{{- printf "%s-%s-%s" .Release.Name .Chart.Name "service" | indent 1 }}
{{- else }}
{{- printf "%s-%s-%s-%s" .Release.Name "exporter" .Chart.Name "service" | indent 1 }}
{{- end }}
{{- end }}

{{/*
Create configmap name as used by the service name label.
*/}}
{{- define "configmap.fullname" -}}
{{- if not .Values.isExporter }}
{{- printf "%s-%s-%s" .Release.Name .Chart.Name "configmap" | indent 1 }}
{{- else }}
{{- printf "%s-%s-%s-%s" .Release.Name "exporter" .Chart.Name "configmap" | indent 1 }}
{{- end }}
{{- end }}

{{/*
Create deployment name as used by the service name label.
*/}}
{{- define "deployment.fullname" -}}
{{- if not .Values.isExporter }}
{{- printf "%s-%s-%s" .Release.Name .Chart.Name "deployment" | indent 1 }}
{{- else }}
{{- printf "%s-%s-%s-%s" .Release.Name "exporter" .Chart.Name "deployment" | indent 1 }}
{{- end }}
{{- end }}

{{/*
Create route name as used by the service name label.
*/}}
{{- define "route.fullname" -}}
{{- if not .Values.isExporter }}
{{- printf "%s-%s-%s" .Release.Name .Chart.Name "route" | indent 1 }}
{{- else }}
{{- printf "%s-%s-%s-%s" .Release.Name "exporter" .Chart.Name "route" | indent 1 }}
{{- end }}
{{- end }}

{{/*
Create ingress name as used by the service name label.
*/}}
{{- define "ingress.fullname" -}}
{{- if not .Values.isExporter }}
{{- printf "%s-%s-%s" .Release.Name .Chart.Name "ingress" | indent 1 }}
{{- else }}
{{- printf "%s-%s-%s-%s" .Release.Name "exporter" .Chart.Name "ingress" | indent 1 }}
{{- end }}
{{- end }}
